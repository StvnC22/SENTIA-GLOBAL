using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.SocialListening.Insights;
using AnalisisSentimiento.Application.SocialListening.Preprocessing;
using AnalisisSentimiento.Application.SocialListening.Search;
using AnalisisSentimiento.Application.SocialListening.Territory;

namespace AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;

public record GetSocialCommentsQuery(
    string? Platform = null,
    bool Refresh = false,
    string? Topic = null,
    string? Prompt = null,
    IReadOnlyDictionary<string, string>? SourceUrls = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    string? Province = null,
    string? Canton = null,
    string? Parish = null
) : IQuery<SocialListeningDashboard>;

internal class GetSocialCommentsQueryHandler(
    ISocialCommentProvider commentProvider,
    ISentimentAnalyzer sentimentAnalyzer,
    TimeProvider timeProvider
) : IRequestHandler<GetSocialCommentsQuery, SocialListeningDashboard>
{
    public async Task<SocialListeningDashboard> Handle(
        GetSocialCommentsQuery request,
        CancellationToken cancellationToken
    )
    {
        var fetchResult = await commentProvider.FetchAsync(
            request.Platform,
            request.Refresh,
            cancellationToken,
            request.Topic,
            request.SourceUrls
        );

        var drafts = CommentPreprocessor
            .Clean(fetchResult.Comments)
            .Select(EnrichTerritory)
            .Where(draft =>
                BooleanQueryMatcher.Matches(
                    request.Topic,
                    $"{draft.Text} {draft.PostTitle} {draft.AuthorHandle}"
                )
            )
            .Where(draft =>
                ManabiTerritoryCatalog.MatchesFilter(
                    draft.Province,
                    draft.Canton,
                    draft.Parish,
                    request.Province,
                    request.Canton,
                    request.Parish
                )
            )
            .Where(draft => MatchesDateRange(draft.PublishedAt, request.FromDate, request.ToDate))
            .ToArray();

        var sentimentRequests = drafts
            .Select(comment => new SentimentAnalysisRequest(comment.Id, comment.Text))
            .ToArray();

        var sentiments = await sentimentAnalyzer.AnalyzeBatchAsync(
            sentimentRequests,
            cancellationToken,
            request.Prompt
        );

        var comments = drafts
            .Select(draft =>
            {
                var sentiment = sentiments.TryGetValue(draft.Id, out var analyzed)
                    ? analyzed
                    : SpanishSentimentAnalyzer.Analyze(draft.Text);

                var enrichment = TextEnrichment.Extract(draft.Text, sentiment.Label);

                return new SocialComment(
                    draft.Id,
                    draft.Platform,
                    draft.AuthorName,
                    draft.AuthorHandle,
                    draft.AuthorUrl,
                    draft.Text,
                    draft.CommentUrl,
                    draft.PostTitle,
                    draft.PostUrl,
                    draft.PublishedAt,
                    sentiment,
                    draft.Province,
                    draft.Canton,
                    draft.Parish,
                    draft.ReactionCount,
                    enrichment.Topics,
                    enrichment.Entities,
                    enrichment.Emotions,
                    enrichment.Aspects
                );
            })
            .Where(comment =>
                string.IsNullOrWhiteSpace(request.Platform)
                || comment.Platform.Equals(request.Platform, StringComparison.OrdinalIgnoreCase)
            )
            .OrderByDescending(comment => comment.PublishedAt)
            .ToArray();

        var summary = new SentimentSummary(
            comments.Length,
            comments.Count(comment => comment.Sentiment.Label == "positive"),
            comments.Count(comment => comment.Sentiment.Label == "neutral"),
            comments.Count(comment => comment.Sentiment.Label == "negative")
        );

        var insights = DashboardInsightBuilder.Build(comments, request.Topic);

        return new SocialListeningDashboard(
            comments,
            summary,
            timeProvider.GetUtcNow(),
            fetchResult.DataSource,
            fetchResult.Message,
            insights
        );
    }

    private static SocialCommentDraft EnrichTerritory(SocialCommentDraft draft)
    {
        if (!string.IsNullOrWhiteSpace(draft.Province) || !string.IsNullOrWhiteSpace(draft.Canton))
        {
            return draft;
        }

        var inferred = ManabiTerritoryCatalog.InferFromText($"{draft.Text} {draft.PostTitle}");
        if (inferred is null)
        {
            return draft;
        }

        return draft with
        {
            Province = inferred.Province,
            Canton = inferred.Canton,
            Parish = inferred.Parish,
        };
    }

    private static bool MatchesDateRange(
        DateTimeOffset publishedAt,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate
    )
    {
        if (fromDate is not null && publishedAt < fromDate)
        {
            return false;
        }

        if (toDate is not null && publishedAt > toDate)
        {
            return false;
        }

        return true;
    }
}

public static class SpanishSentimentAnalyzer
{
    private static readonly string[] PositiveTerms =
    [
        "excelente",
        "gracias",
        "gran",
        "lindo",
        "linda",
        "mejor",
        "orgullo",
        "orgulloso",
        "orgullosa",
        "apoyar",
        "apoyo",
        "felicit",
        "maravilloso",
        "hermoso",
        "éxito",
        "exito",
        "bienvenido",
        "bienvenida",
        "clara",
        "amor",
        "bravo",
        "increíble",
        "increible",
    ];

    private static readonly string[] NegativeTerms =
    [
        "imposible",
        "mala",
        "malo",
        "malísimo",
        "no funciona",
        "no responden",
        "otra vez",
        "pésame",
        "pesame",
        "condolencia",
        "pérdida",
        "perdida",
        "muerte",
        "falleci",
        "triste",
        "dolor",
        "qdd",
        "q.e.p.d",
        "qepd",
        "descansa",
        "paz en",
        "vergüenza",
        "verguenza",
        "denuncia",
        "corrupci",
        "pésima",
        "pesima",
    ];

    public static SentimentResult Analyze(string text)
    {
        var normalizedText = text.ToLowerInvariant();
        var passiveAggressiveSignals = new[]
        {
            "gracias por nada",
            "qué sorpresa",
            "que sorpresa",
            "como siempre",
            "de mal en peor",
            "muy bien, pero",
            "lamentable que",
            "lamentable esto",
            "no confirmaron",
            "no respondieron",
            "no las entregaron",
            "no lo entregaron",
            "no entregaron",
            "no me dieron",
            "no nos dieron",
            "no recibí",
            "no recibimos",
            "no llegó",
            "no llegaron",
            "pedido incompleto",
            "faltaron",
            "ni siquiera",
            "más nada",
        };
        if (passiveAggressiveSignals.Any(normalizedText.Contains))
        {
            return new SentimentResult("negative", "Negativo", 84);
        }
        var positiveScore = PositiveTerms.Count(normalizedText.Contains);
        var negativeScore = NegativeTerms.Count(normalizedText.Contains);

        if (positiveScore > negativeScore)
        {
            return new SentimentResult(
                "positive",
                "Positivo",
                Math.Min(96, 78 + positiveScore * 6)
            );
        }

        if (negativeScore > positiveScore)
        {
            return new SentimentResult(
                "negative",
                "Negativo",
                Math.Min(96, 78 + negativeScore * 6)
            );
        }

        return new SentimentResult("neutral", "Neutral", 74);
    }
}

public static class TextEnrichment
{
    private static readonly (string Topic, string[] Signals)[] TopicLexicon =
    [
        (
            "educación",
            [
                "universidad",
                "carrera",
                "matrícula",
                "matricula",
                "admisiones",
                "estudiante",
                "académ",
            ]
        ),
        ("servicio", ["trámite", "tramite", "atienden", "responden", "sistema", "plataforma"]),
        (
            "infraestructura",
            ["campus", "aulas", "edificio", "laboratorio", "extensión", "extension"]
        ),
        ("investigación", ["investigación", "investigacion", "convenio", "proyecto"]),
        ("beca", ["beca", "ayuda", "beneficio", "financiamiento"]),
    ];

    private static readonly (string Emotion, string[] Signals)[] EmotionLexicon =
    [
        ("alegría", ["orgullo", "feliz", "gracias", "lindo", "excelente", "amor"]),
        ("enojo", ["indign", "vergüenza", "verguenza", "pésima", "pesima", "denuncia"]),
        ("tristeza", ["triste", "dolor", "pésame", "condolencia", "pérdida"]),
        ("confianza", ["claro", "clara", "confi", "seguro", "apoyo"]),
        ("preocupación", ["urgente", "imposible", "no funciona", "otra vez", "necesito"]),
    ];

    private static readonly string[] ClauseSeparators =
    [
        ".",
        "!",
        "?",
        ";",
        ",",
        " pero ",
        " aunque ",
        " sin embargo ",
    ];

    public static TextEnrichmentResult Extract(string text, string? overallSentimentLabel = null)
    {
        var lower = text.ToLowerInvariant();
        var matchedTopics = TopicLexicon
            .Where(entry =>
                entry.Signals.Any(signal => lower.Contains(signal, StringComparison.Ordinal))
            )
            .ToArray();
        var topics = matchedTopics.Select(entry => entry.Topic).Distinct().ToArray();

        var clauses = lower.Split(
            ClauseSeparators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        var aspects = matchedTopics
            .Select(entry =>
            {
                var clauseLabels = clauses
                    .Where(clause =>
                        entry.Signals.Any(signal =>
                            clause.Contains(signal, StringComparison.Ordinal)
                        )
                    )
                    .Select(clause => SpanishSentimentAnalyzer.Analyze(clause).Label)
                    .ToArray();

                var label =
                    clauseLabels.Contains("negative") ? "negative"
                    : clauseLabels.Contains("positive") ? "positive"
                    : matchedTopics.Length == 1 && !string.IsNullOrWhiteSpace(overallSentimentLabel)
                        ? overallSentimentLabel
                    : "neutral";

                return new AspectSentiment(entry.Topic, label);
            })
            .ToArray();

        var emotions = EmotionLexicon
            .Where(entry =>
                entry.Signals.Any(signal => lower.Contains(signal, StringComparison.Ordinal))
            )
            .Select(entry => entry.Emotion)
            .Distinct()
            .ToArray();

        var entities = new List<string>();
        if (lower.Contains("uleam", StringComparison.Ordinal))
            entities.Add("ULEAM");
        if (lower.Contains("manab", StringComparison.Ordinal))
            entities.Add("Manabí");
        foreach (var canton in ManabiTerritoryCatalog.CantonNames)
        {
            if (lower.Contains(canton.ToLowerInvariant(), StringComparison.Ordinal))
            {
                entities.Add(canton);
            }
        }

        return new TextEnrichmentResult(topics, entities.Distinct().ToArray(), emotions, aspects);
    }
}

public record TextEnrichmentResult(
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> Entities,
    IReadOnlyList<string> Emotions,
    IReadOnlyList<AspectSentiment> Aspects
);

public record AspectSentiment(string Aspect, string Label);

public record SocialListeningDashboard(
    IReadOnlyCollection<SocialComment> Comments,
    SentimentSummary Summary,
    DateTimeOffset UpdatedAt,
    string DataSource = "demo",
    string? Message = null,
    DashboardInsights? Insights = null
);

public record SocialComment(
    string Id,
    string Platform,
    string AuthorName,
    string AuthorHandle,
    string AuthorUrl,
    string Text,
    string CommentUrl,
    string PostTitle,
    string PostUrl,
    DateTimeOffset PublishedAt,
    SentimentResult Sentiment = null!,
    string? Province = null,
    string? Canton = null,
    string? Parish = null,
    int ReactionCount = 0,
    IReadOnlyList<string>? Topics = null,
    IReadOnlyList<string>? Entities = null,
    IReadOnlyList<string>? Emotions = null,
    IReadOnlyList<AspectSentiment>? Aspects = null
);

public record SentimentResult(string Label, string DisplayName, int Confidence);

public record SentimentSummary(int Total, int Positive, int Neutral, int Negative);

public record DashboardInsights(
    IReadOnlyList<TemporalTrendPoint> TemporalTrend,
    IReadOnlyList<GeoMentionRank> GeoRanking,
    IReadOnlyList<WordCloudTerm> WordCloud,
    string ExecutiveSummary,
    IReadOnlyList<NetworkSentiment> NetworkBreakdown,
    IReadOnlyList<AspectSummary> AspectBreakdown,
    IReadOnlyList<LabelCount> EmotionBreakdown,
    IReadOnlyList<LabelCount> TopEntities,
    int TotalReactions
);

public record NetworkSentiment(
    string Platform,
    int Total,
    int Positive,
    int Neutral,
    int Negative,
    int Reactions
);

public record AspectSummary(string Aspect, int Mentions, int Positive, int Neutral, int Negative);

public record LabelCount(string Label, int Count);

public record TemporalTrendPoint(DateTime Date, int Total, int Positive, int Neutral, int Negative);

public record GeoMentionRank(
    string Province,
    string? Canton,
    string? Parish,
    int Mentions,
    int Positive,
    int Neutral,
    int Negative
);

public record WordCloudTerm(string Term, int Count);
