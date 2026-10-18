using System.Text;
using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;

namespace AnalisisSentimiento.Application.SocialListening.Insights;

public static class DashboardInsightBuilder
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "al",
        "algo",
        "como",
        "con",
        "de",
        "del",
        "desde",
        "el",
        "en",
        "es",
        "esta",
        "este",
        "la",
        "las",
        "lo",
        "los",
        "más",
        "me",
        "mi",
        "muy",
        "no",
        "nos",
        "o",
        "para",
        "pero",
        "por",
        "que",
        "se",
        "sin",
        "sobre",
        "su",
        "sus",
        "un",
        "una",
        "uno",
        "y",
        "ya",
        "yo",
        "the",
        "and",
    };

    public static DashboardInsights Build(IReadOnlyList<SocialComment> comments, string? topic)
    {
        var byDay = comments
            .GroupBy(c => c.PublishedAt.ToOffset(TimeSpan.FromHours(-5)).Date)
            .OrderBy(g => g.Key)
            .Select(g => new TemporalTrendPoint(
                g.Key,
                g.Count(),
                g.Count(c => c.Sentiment.Label == "positive"),
                g.Count(c => c.Sentiment.Label == "neutral"),
                g.Count(c => c.Sentiment.Label == "negative")
            ))
            .ToArray();

        var geoRanking = comments
            .Where(c =>
                !string.IsNullOrWhiteSpace(c.Canton) || !string.IsNullOrWhiteSpace(c.Province)
            )
            .GroupBy(c =>
                string.IsNullOrWhiteSpace(c.Canton) ? c.Province! : $"{c.Province}/{c.Canton}"
            )
            .Select(g => new GeoMentionRank(
                g.First().Province ?? "Manabí",
                g.First().Canton,
                g.First().Parish,
                g.Count(),
                g.Count(c => c.Sentiment.Label == "positive"),
                g.Count(c => c.Sentiment.Label == "neutral"),
                g.Count(c => c.Sentiment.Label == "negative")
            ))
            .OrderByDescending(r => r.Mentions)
            .Take(12)
            .ToArray();

        var wordCloud = BuildWordCloud(comments);
        var executiveSummary = BuildExecutiveSummary(comments, topic, byDay, geoRanking);

        return new DashboardInsights(byDay, geoRanking, wordCloud, executiveSummary);
    }

    private static IReadOnlyList<WordCloudTerm> BuildWordCloud(IEnumerable<SocialComment> comments)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var comment in comments)
        {
            var text = $"{comment.Text} {comment.PostTitle}".ToLowerInvariant();
            foreach (
                System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                    text,
                    @"[#]?[a-záéíóúñü]{4,}"
                )
            )
            {
                var word = match
                    .Value.Normalize(NormalizationForm.FormD)
                    .Where(c =>
                        System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.NonSpacingMark
                    )
                    .Aggregate(new StringBuilder(), (b, c) => b.Append(char.ToLowerInvariant(c)))
                    .ToString();

                if (StopWords.Contains(word) || word.StartsWith("http", StringComparison.Ordinal))
                {
                    continue;
                }

                counts[word] = counts.GetValueOrDefault(word) + 1;
            }
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .Take(40)
            .Select(kv => new WordCloudTerm(kv.Key, kv.Value))
            .ToArray();
    }

    private static string BuildExecutiveSummary(
        IReadOnlyList<SocialComment> comments,
        string? topic,
        IReadOnlyList<TemporalTrendPoint> trend,
        IReadOnlyList<GeoMentionRank> geo
    )
    {
        if (comments.Count == 0)
        {
            return "No hay opiniones recolectadas para el criterio actual. Ajuste la búsqueda, el territorio o el rango de fechas.";
        }

        var total = comments.Count;
        var positive = comments.Count(c => c.Sentiment.Label == "positive");
        var neutral = comments.Count(c => c.Sentiment.Label == "neutral");
        var negative = comments.Count(c => c.Sentiment.Label == "negative");
        var dominant = new[]
        {
            ("positivo", positive),
            ("neutral", neutral),
            ("negativo", negative),
        }
            .OrderByDescending(x => x.Item2)
            .First();

        var topicLabel = string.IsNullOrWhiteSpace(topic)
            ? "el tema monitoreado"
            : $"«{topic.Trim()}»";
        var networks = string.Join(
            ", ",
            comments
                .Select(c => c.Platform)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
        );

        var trendNote = "Sin variación temporal suficiente.";
        if (trend.Count >= 2)
        {
            var first = trend.First();
            var last = trend.Last();
            var delta = last.Total - first.Total;
            trendNote =
                delta > 0
                    ? $"La actividad reciente creció ({first.Total} → {last.Total} menciones en el último tramo)."
                : delta < 0
                    ? $"La actividad reciente disminuyó ({first.Total} → {last.Total} menciones en el último tramo)."
                : "La actividad se mantuvo estable en el periodo analizado.";
        }

        var geoNote =
            geo.Count > 0
                ? $"El territorio con más menciones es {geo[0].Province}"
                    + (string.IsNullOrWhiteSpace(geo[0].Canton) ? "" : $" / {geo[0].Canton}")
                    + $" ({geo[0].Mentions})."
                : "No se detectaron ubicaciones geográficas explícitas en los textos.";

        return $"Resumen ejecutivo del Observatorio Territorial: se analizaron {total} opiniones públicas sobre {topicLabel} "
            + $"en {networks}. El sentimiento dominante es {dominant.Item1} "
            + $"({Pct(dominant.Item2, total)}%), con {Pct(positive, total)}% positivo, {Pct(neutral, total)}% neutro y {Pct(negative, total)}% negativo. "
            + $"{trendNote} {geoNote}";
    }

    private static int Pct(int value, int total) =>
        total == 0 ? 0 : (int)Math.Round(value * 100.0 / total);
}
