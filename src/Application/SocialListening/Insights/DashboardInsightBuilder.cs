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

        var networkBreakdown = comments
            .GroupBy(c => c.Platform.ToLowerInvariant())
            .Select(g => new NetworkSentiment(
                g.Key,
                g.Count(),
                g.Count(c => c.Sentiment.Label == "positive"),
                g.Count(c => c.Sentiment.Label == "neutral"),
                g.Count(c => c.Sentiment.Label == "negative"),
                g.Sum(c => c.ReactionCount)
            ))
            .OrderByDescending(n => n.Total)
            .ToArray();

        var aspectBreakdown = comments
            .SelectMany(c => c.Aspects ?? [])
            .GroupBy(a => a.Aspect)
            .Select(g => new AspectSummary(
                g.Key,
                g.Count(),
                g.Count(a => a.Label == "positive"),
                g.Count(a => a.Label == "neutral"),
                g.Count(a => a.Label == "negative")
            ))
            .OrderByDescending(a => a.Mentions)
            .ToArray();

        var emotionBreakdown = CountLabels(comments.SelectMany(c => c.Emotions ?? []), 8);
        var topEntities = CountLabels(comments.SelectMany(c => c.Entities ?? []), 10);

        var wordCloud = BuildWordCloud(comments);
        var executiveSummary = BuildExecutiveSummary(
            comments,
            topic,
            byDay,
            geoRanking,
            networkBreakdown,
            aspectBreakdown,
            emotionBreakdown,
            wordCloud
        );

        return new DashboardInsights(
            byDay,
            geoRanking,
            wordCloud,
            executiveSummary,
            networkBreakdown,
            aspectBreakdown,
            emotionBreakdown,
            topEntities,
            comments.Sum(c => c.ReactionCount)
        );
    }

    private static IReadOnlyList<LabelCount> CountLabels(IEnumerable<string> labels, int take) =>
        labels
            .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
            .Select(g => new LabelCount(g.First(), g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(take)
            .ToArray();

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
        IReadOnlyList<GeoMentionRank> geo,
        IReadOnlyList<NetworkSentiment> networks,
        IReadOnlyList<AspectSummary> aspects,
        IReadOnlyList<LabelCount> emotions,
        IReadOnlyList<WordCloudTerm> terms
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
        var networkFrequencies = string.Join(
            ", ",
            networks.Select(n =>
                $"{NetworkName(n.Platform)} {n.Total} ({Pct(n.Negative, n.Total)}% negativo)"
            )
        );

        var trendNote = "Sin variación temporal suficiente.";
        if (trend.Count >= 2)
        {
            var first = trend.First();
            var last = trend.Last();
            var delta = last.Total - first.Total;
            trendNote =
                delta > 0
                    ? $"La actividad reciente creció (de {first.Total} a {last.Total} menciones diarias entre el primer y el último día)."
                : delta < 0
                    ? $"La actividad reciente disminuyó (de {first.Total} a {last.Total} menciones diarias entre el primer y el último día)."
                : "La actividad se mantuvo estable en el periodo analizado.";
        }

        var geoNote =
            geo.Count > 0
                ? $"El territorio con más menciones es {geo[0].Province}"
                    + (string.IsNullOrWhiteSpace(geo[0].Canton) ? "" : $" / {geo[0].Canton}")
                    + $" ({geo[0].Mentions})."
                : "No se detectaron ubicaciones geográficas explícitas en los textos.";

        var criticalAspect = aspects
            .Where(a => a.Negative > 0)
            .OrderByDescending(a => a.Negative)
            .ThenByDescending(a => a.Mentions)
            .FirstOrDefault();
        var aspectNote =
            aspects.Count == 0
                ? "No se identificaron aspectos temáticos específicos."
                : $"Aspectos más comentados: {string.Join(", ", aspects.Take(3).Select(a => $"{a.Aspect} ({a.Mentions})"))}."
                    + (
                        criticalAspect is null
                            ? ""
                            : $" El aspecto con más críticas es {criticalAspect.Aspect} ({criticalAspect.Negative} menciones negativas)."
                    );
        var emotionNote =
            emotions.Count == 0
                ? ""
                : $" Emoción predominante: {emotions[0].Label} ({emotions[0].Count}).";
        var termsNote =
            terms.Count == 0
                ? ""
                : $" Términos más frecuentes: {string.Join(", ", terms.Take(5).Select(t => $"{t.Term} ({t.Count})"))}.";

        return $"Resumen ejecutivo del Observatorio Territorial: se analizaron {total} opiniones públicas sobre {topicLabel}. "
            + $"Frecuencia por red: {networkFrequencies}. El sentimiento dominante es {dominant.Item1} "
            + $"({Pct(dominant.Item2, total)}%), con {Pct(positive, total)}% positivo, {Pct(neutral, total)}% neutro y {Pct(negative, total)}% negativo. "
            + $"{trendNote} {geoNote} {aspectNote}{emotionNote}{termsNote}";
    }

    private static string NetworkName(string platform) =>
        platform.ToLowerInvariant() switch
        {
            "instagram" => "Instagram",
            "facebook" => "Facebook",
            "tiktok" => "TikTok",
            "x" => "X",
            _ => platform,
        };

    private static int Pct(int value, int total) =>
        total == 0 ? 0 : (int)Math.Round(value * 100.0 / total);
}
