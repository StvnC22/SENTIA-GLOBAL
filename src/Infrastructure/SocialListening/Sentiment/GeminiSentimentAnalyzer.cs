using System.Net.Http.Json;
using System.Text.Json;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;
using AnalisisSentimiento.Infrastructure.SocialListening.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.SocialListening.Sentiment;

public sealed class GeminiSentimentAnalyzer(
    HttpClient httpClient,
    IApiCredentialStore credentialStore,
    IOptions<SocialListeningOptions> options,
    LocalSentimentAnalyzer localSentimentAnalyzer,
    ILogger<GeminiSentimentAnalyzer> logger
) : ISentimentAnalyzer
{
    private const int BatchSize = 30;
    private const int MaxParallelBatches = 3;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyDictionary<string, SentimentResult>> AnalyzeBatchAsync(
        IReadOnlyList<SentimentAnalysisRequest> comments,
        CancellationToken cancellationToken,
        string? prompt = null
    )
    {
        if (comments.Count == 0)
        {
            return new Dictionary<string, SentimentResult>();
        }

        var credentials = await credentialStore.GetAsync(cancellationToken);
        var apiKey = credentials.ArtificialIntelligence?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return await localSentimentAnalyzer.AnalyzeBatchAsync(comments, cancellationToken, prompt);
        }

        var results = new Dictionary<string, SentimentResult>(StringComparer.OrdinalIgnoreCase);
        var batches = comments.Chunk(BatchSize).ToArray();
        using var gate = new SemaphoreSlim(MaxParallelBatches, MaxParallelBatches);
        var tasks = batches.Select(async batch =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await AnalyzeBatchWithGeminiAsync(apiKey, batch, cancellationToken, prompt);
            }
            finally
            {
                gate.Release();
            }
        });

        foreach (var analyzed in await Task.WhenAll(tasks))
        {
            foreach (var (id, sentiment) in analyzed)
            {
                results[id] = sentiment;
            }
        }

        foreach (var comment in comments.Where(comment => !results.ContainsKey(comment.Id)))
        {
            results[comment.Id] = SpanishSentimentAnalyzer.Analyze(comment.Text);
        }

        return results;
    }

    private async Task<IReadOnlyDictionary<string, SentimentResult>> AnalyzeBatchWithGeminiAsync(
        string apiKey,
        IReadOnlyList<SentimentAnalysisRequest> comments,
        CancellationToken cancellationToken,
        string? prompt = null
    )
    {
        if (comments.Count == 0)
        {
            return new Dictionary<string, SentimentResult>();
        }

        var models = new[]
        {
            options.Value.Gemini.Model,
            "gemini-2.5-flash-lite",
            "gemini-2.5-flash",
        }
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var model in models)
        {
            try
            {
                var analyzed = await TryAnalyzeWithModelAsync(
                    apiKey,
                    model,
                    comments,
                    cancellationToken,
                    prompt
                );
                if (analyzed is not null)
                {
                    return analyzed;
                }
            }
            catch (Exception exception)
                when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                logger.LogWarning(
                    exception,
                    "Gemini modelo {Model} falló; se prueba el siguiente.",
                    model
                );
            }
        }

        logger.LogWarning("Gemini no respondió para un lote; se usa el analizador local.");
        return await localSentimentAnalyzer.AnalyzeBatchAsync(comments, cancellationToken, prompt);
    }

    private async Task<IReadOnlyDictionary<string, SentimentResult>?> TryAnalyzeWithModelAsync(
        string apiKey,
        string model,
        IReadOnlyList<SentimentAnalysisRequest> comments,
        CancellationToken cancellationToken,
        string? prompt = null
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{model}:generateContent"
        );
        request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(
            new
            {
                contents = new[] { new { parts = new[] { new { text = BuildPrompt(comments, prompt) } } } },
                generationConfig = new
                {
                    temperature = 0.1,
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                id = new { type = "STRING" },
                                label = new { type = "STRING", @enum = new[] { "positive", "neutral", "negative" } },
                                displayName = new { type = "STRING" },
                                confidence = new { type = "INTEGER", minimum = 1, maximum = 100 },
                            },
                            required = new[] { "id", "label", "displayName", "confidence" },
                            additionalProperties = false,
                        },
                    },
                },
            }
        );

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Gemini {Model} respondió {StatusCode}: {Body}",
                model,
                response.StatusCode,
                body.Length > 240 ? body[..240] : body
            );
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var responseText = ExtractResponseText(payload);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        var json = UnwrapJson(responseText);
        var parsed =
            JsonSerializer.Deserialize<GeminiSentimentResponse[]>(json, JsonOptions) ?? [];

        if (parsed.Length == 0)
        {
            return null;
        }

        var validIds = comments
            .Select(comment => comment.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return parsed
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && validIds.Contains(item.Id))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToDictionary(
                item => item.Id,
                item => new SentimentResult(
                    NormalizeLabel(item.Label),
                    NormalizeDisplayName(item.Label, item.DisplayName),
                    CalibrateConfidence(
                        item.Confidence,
                        NormalizeLabel(item.Label),
                        comments.First(comment => comment.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase)).Text
                    )
                ),
                StringComparer.OrdinalIgnoreCase
            );
    }

    private static string BuildPrompt(
        IReadOnlyList<SentimentAnalysisRequest> comments,
        string? customPrompt
    )
    {
        var serializedComments = JsonSerializer.Serialize(
            comments.Select(comment => new { id = comment.Id, text = comment.Text })
        );

        var instructions = string.IsNullOrWhiteSpace(customPrompt)
            ? """
              PROTOCOLO EXPERTO PARA ESPAÑOL Y REDES SOCIALES
              Clasifica cada comentario únicamente por lo que comunica el texto. No uses el nombre del autor, la cuenta, la red social, el tema de la publicación ni conocimientos externos. No inventes hechos.

              PASO 1 — LECTURA Y CONTEXTO:
              - Lee el comentario completo, incluyendo menciones, hashtags, emojis, signos, abreviaturas, errores ortográficos, mayúsculas y repeticiones.
              - Interpreta lenguaje informal de redes: "jajaja", "xd", "lol", alargamientos, emojis y expresiones regionales.
              - Identifica el objetivo: elogiar/apoyar, reclamar/denunciar, preguntar/informar o una combinación.
              - Distingue objetivo y sentimiento: una pregunta puede ser negativa si contiene un reclamo; una petición amable puede ser neutral.

              PASO 2 — ETIQUETAS:
              - positive: aprobación, satisfacción, alegría, agradecimiento, felicitación, apoyo, confianza, esperanza, recomendación o elogio sincero. Incluye emojis claramente favorables.
              - negative: queja, incumplimiento, mala experiencia, frustración, enojo, decepción, rechazo, denuncia, acusación, insulto, amenaza, discriminación, tristeza, daño, pérdida o reclamo de atención. Incluye problemas concretos aunque se expresen con cortesía: "no me respondieron", "no entregaron el pedido", "sigo esperando", "nadie ayuda".
              - neutral: información factual, anuncio, saludo, pregunta informativa, solicitud de precio/horario/enlace o descripción sin aprobación ni desaprobación. No uses neutral para evitar decidir cuando existe una señal emocional.

              PASO 3 — CASOS DIFÍCILES:
              - Negación: interpreta "no", "nunca", "jamás" y "ni siquiera" en el contexto completo.
              - Contraste: en "el producto es bueno, pero nunca llegó" domina negative por el incumplimiento.
              - Pregunta con reclamo: "¿cuándo van a solucionar esto?" es negative; "¿cuál es el horario?" es neutral.
              - Pasivo-agresividad: "gracias por nada", "como siempre", "qué sorpresa", "felicidades por no responder" y reproches indirectos son negative cuando desaprueban o denuncian.
              - Sarcasmo/ironía: no tomes literalmente una palabra positiva si el contexto la contradice. "Qué excelente servicio 🙄, otra vez nadie responde" es negative.
              - Emojis: 😍🎉👏❤️ suelen apoyar; 😡😤😒🙄🤦😢💔 suelen expresar molestia o tristeza. Evalúalos junto al texto, nunca aislados.
              - Cortesía no elimina el problema: "por favor, necesito que corrijan el cobro" es negative.
              - Sentimientos mixtos: selecciona la polaridad dominante según el problema o valoración final.
              - Ambigüedad real, texto demasiado corto o ironía no demostrable: usa neutral y confianza baja; no inventes intención.

              EJEMPLOS:
              - "Me encantó la atención, volveré" → positive.
              - "Gracias por nada, llevo tres semanas esperando" → negative.
              - "¿Cuál es el horario de atención?" → neutral.
              - "El producto es bueno, pero nunca me lo entregaron" → negative.
              - "Qué excelente servicio 🙄, otra vez nadie responde" → negative.
              - "Necesito el enlace para registrarme" → neutral.

              CONFIANZA CALIBRADA:
              - 95-100: evidencia explícita, inequívoca y coherente; solo en casos muy claros.
              - 85-94: polaridad clara con señales directas, aunque haya lenguaje informal.
              - 70-84: interpretación bastante probable, con mezcla o contexto implícito.
              - 50-69: ambigüedad, ironía posible, texto corto o señales contradictorias.
              - 1-49: casi no hay evidencia de polaridad; normalmente neutral.
              La confianza estima la evidencia del texto, no garantiza el acierto. No la subas para aparentar precisión.
              """
            : customPrompt.Trim().Length > 1500
                ? customPrompt.Trim()[..1500]
                : customPrompt.Trim();

        return $"""
            Eres un sistema experto de análisis de sentimiento en español para comentarios públicos de redes sociales.
            Tu tarea es clasificar cada entrada de forma independiente como positive, neutral o negative.
            La clasificación debe basarse exclusivamente en el texto recibido y en el protocolo siguiente.

            PROTOCOLO:
            {instructions}

            REGLAS DE SALIDA:
            - Devuelve exactamente un resultado por cada comentario de entrada y conserva su id sin cambios.
            - Devuelve únicamente un arreglo JSON válido, sin Markdown, sin explicaciones y sin texto antes o después.
            - Cada objeto debe contener exactamente estos campos: id, label, displayName y confidence.
            - label solo puede ser positive, neutral o negative.
            - displayName debe ser exactamente Positivo, Neutral o Negativo y corresponder a label.
            - confidence debe ser un entero entre 1 y 100; no escribas porcentajes ni decimales.
            - Devuelve exactamente el mismo número de objetos que comentarios recibidos y no repitas ids.

            Comentarios a clasificar:
            {serializedComments}
            """;
    }

    private static string UnwrapJson(string responseText)
    {
        var trimmed = responseText.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var start = trimmed.IndexOf('{', StringComparison.Ordinal);
            var arrayStart = trimmed.IndexOf('[', StringComparison.Ordinal);
            var open =
                arrayStart >= 0 && (start < 0 || arrayStart < start)
                    ? arrayStart
                    : Math.Max(start, arrayStart);
            var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (open >= 0 && end > open)
            {
                return trimmed[open..end].Trim();
            }
        }

        return trimmed;
    }

    private static string ExtractResponseText(JsonElement body)
    {
        if (
            body.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array
        )
        {
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (
                    candidate.TryGetProperty("content", out var content)
                    && content.TryGetProperty("parts", out var parts)
                    && parts.ValueKind == JsonValueKind.Array
                )
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (
                            part.TryGetProperty("text", out var text)
                            && text.ValueKind == JsonValueKind.String
                        )
                        {
                            return text.GetString() ?? string.Empty;
                        }
                    }
                }
            }
        }

        return string.Empty;
    }

    private static string NormalizeLabel(string? label) =>
        label?.Trim().ToLowerInvariant() switch
        {
            "positive" or "positivo" => "positive",
            "negative" or "negativo" => "negative",
            _ => "neutral",
        };

    private static int CalibrateConfidence(int modelConfidence, string label, string text)
    {
        var confidence = Math.Clamp(modelConfidence, 1, 100);
        var normalized = text.Trim().ToLowerInvariant();
        var hasContrast = new[] { " pero ", " aunque ", " sin embargo ", " no obstante " }
            .Any(normalized.Contains);
        var hasUncertainty = new[] { "quizá", "quizas", "tal vez", "no sé", "no se", "parece" }
            .Any(normalized.Contains);
        var hasDirectNegativeSignal = new[]
        {
            "no entreg", "no respond", "no recib", "no funciona", "lamentable",
            "gracias por nada", "como siempre", "qué sorpresa", "que sorpresa",
            "vergüenza", "verguenza", "pésimo", "pesimo", "estafa", "denuncia"
        }.Any(normalized.Contains);

        // A model must not report near-certainty for a short, mixed or
        // explicitly uncertain message. Conversely, clear complaints should
        // not be artificially downgraded merely because they are polite.
        if (normalized.Length < 18 || hasUncertainty)
        {
            confidence = Math.Min(confidence, 68);
        }

        if (hasContrast)
        {
            confidence = Math.Min(confidence, 84);
        }

        if (label == "negative" && hasDirectNegativeSignal)
        {
            confidence = Math.Max(confidence, 84);
        }

        return Math.Clamp(confidence, 1, 100);
    }

    private static string NormalizeDisplayName(string? label, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return NormalizeLabel(label) switch
        {
            "positive" => "Positivo",
            "negative" => "Negativo",
            _ => "Neutral",
        };
    }

    private sealed record GeminiSentimentResponse(
        string Id,
        string Label,
        string DisplayName,
        int Confidence
    );
}
