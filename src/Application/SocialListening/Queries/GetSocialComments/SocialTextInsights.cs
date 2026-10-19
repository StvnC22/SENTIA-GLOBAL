using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;

public sealed record SocialTextInsights(
    string Emotion,
    IReadOnlyCollection<string> Topics,
    IReadOnlyCollection<string> Aspects,
    IReadOnlyCollection<string> Entities
);

public static class SocialTextInsightsAnalyzer
{
    private static readonly Dictionary<string, string[]> EmotionSignals = new()
    {
        ["alegría"] =
        [
            "feliz",
            "alegría",
            "excelente",
            "me encanta",
            "orgullo",
            "gracias",
            "❤️",
            "😊",
            "🎉",
        ],
        ["enojo"] =
        [
            "enojo",
            "molesto",
            "indignante",
            "vergüenza",
            "corrupción",
            "estafa",
            "😡",
            "🤬",
        ],
        ["tristeza"] = ["triste", "dolor", "pérdida", "falleció", "condolencias", "😢", "💔"],
        ["confianza"] = ["confío", "seguro", "transparente", "apoyo", "respaldo", "👏"],
        ["temor"] = ["miedo", "temor", "inseguro", "peligro", "amenaza", "😨"],
    };

    private static readonly Dictionary<string, string[]> AspectSignals = new()
    {
        ["docencia"] =
        [
            "docencia",
            "docente",
            "profesor",
            "clase",
            "enseñanza",
            "estudiante",
            "académico",
        ],
        ["infraestructura"] =
        [
            "edificio",
            "aula",
            "campus",
            "laboratorio",
            "infraestructura",
            "baño",
        ],
        ["atención"] = ["atención", "respuesta", "servicio", "trámite", "secretaría", "matrícula"],
        ["seguridad"] = ["seguridad", "robo", "policía", "peligro", "acoso", "violencia"],
        ["gestión"] =
        [
            "rector",
            "administración",
            "gestión",
            "presupuesto",
            "autoridad",
            "decisión",
        ],
    };

    private static readonly string[] KnownEntities = ["ULEAM", "Manta", "Manabí", "Ecuador"];

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "para",
        "pero",
        "porque",
        "desde",
        "donde",
        "cuando",
        "como",
        "esta",
        "este",
        "estos",
        "estas",
        "tiene",
        "tienen",
        "hace",
        "sobre",
        "entre",
        "también",
        "solo",
        "todo",
        "toda",
        "todos",
        "todas",
        "muy",
        "más",
        "menos",
        "hay",
        "han",
        "ser",
        "fue",
        "son",
        "del",
        "las",
        "los",
        "una",
        "uno",
        "unos",
        "unas",
        "que",
        "con",
        "por",
        "sin",
        "sus",
        "les",
        "nos",
        "ellos",
        "ellas",
        "ella",
        "él",
        "usted",
        "ustedes",
        "mi",
        "mis",
        "tu",
        "tus",
        "se",
        "me",
        "te",
        "le",
        "lo",
        "ya",
        "si",
        "no",
        "es",
        "en",
        "de",
        "la",
        "el",
        "y",
        "a",
        "un",
    };

    public static SocialTextInsights Analyze(string text)
    {
        var normalized = Normalize(text);
        var emotion = EmotionSignals
            .Select(pair => new
            {
                pair.Key,
                Score = pair.Value.Count(signal =>
                    normalized.Contains(Normalize(signal), StringComparison.Ordinal)
                ),
            })
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();
        var topics = Regex
            .Matches(text.ToLowerInvariant(), @"#?[\p{L}][\p{L}\p{N}_]{3,}")
            .Select(match => match.Value.TrimStart('#'))
            .Where(word => !StopWords.Contains(word))
            .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(group => group.Key)
            .ToArray();
        var aspects = AspectSignals
            .Where(pair =>
                pair.Value.Any(signal =>
                    normalized.Contains(Normalize(signal), StringComparison.Ordinal)
                )
            )
            .Select(pair => pair.Key)
            .ToArray();
        var entities = KnownEntities
            .Where(entity => normalized.Contains(Normalize(entity), StringComparison.Ordinal))
            .Concat(Regex.Matches(text, @"(?<!\w)#[\p{L}\p{N}_]{3,}").Select(match => match.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SocialTextInsights(
            emotion?.Score > 0 ? emotion.Key : "sin determinar",
            topics,
            aspects,
            entities
        );
    }

    public static bool IsLikelySpam(string text) =>
        text.Trim().Length < 2
        || Regex.IsMatch(text, @"(?i)(gana|premio|gratis|inversión garantizada).{0,60}https?://")
        || Regex.IsMatch(text, @"(?i)https?://\S+\s+https?://\S+\s+https?://\S+");

    public static bool IsLikelyAutomated(int authorCommentCount, int repeatedTextCount) =>
        authorCommentCount >= 10 && repeatedTextCount >= 5;

    private static string Normalize(string value) =>
        value
            .Normalize(NormalizationForm.FormD)
            .Where(character =>
                CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
            )
            .Aggregate(
                new StringBuilder(),
                (builder, character) => builder.Append(char.ToLowerInvariant(character))
            )
            .ToString();
}
