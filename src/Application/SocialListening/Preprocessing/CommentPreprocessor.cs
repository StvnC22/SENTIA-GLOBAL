using System.Text;
using System.Text.RegularExpressions;
using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.SocialListening.Preprocessing;

public static partial class CommentPreprocessor
{
    private static readonly string[] SpamSignals =
    [
        "gana dinero",
        "haz clic aqui",
        "haz clic aquí",
        "crypto free",
        "bitcoin gratis",
        "solo por hoy",
        "http://bit.ly",
        "t.me/",
        "whatsapp.me",
        "compra ahora",
        "oferta limitada 100%",
        "seguidores baratos",
    ];

    private static readonly Regex RepeatedCharRegex = new(@"(.)\1{5,}", RegexOptions.Compiled);
    private static readonly Regex MostlySymbolsRegex = new(@"^[\W\d_]{8,}$", RegexOptions.Compiled);

    public static IReadOnlyList<SocialCommentDraft> Clean(IEnumerable<SocialCommentDraft> drafts)
    {
        var unique = new Dictionary<string, SocialCommentDraft>(StringComparer.OrdinalIgnoreCase);
        foreach (var draft in drafts)
        {
            if (IsSpamOrBot(draft))
            {
                continue;
            }

            var cleanedText = CleanText(draft.Text);
            if (string.IsNullOrWhiteSpace(cleanedText))
            {
                continue;
            }

            var key = BuildDedupKey(draft, cleanedText);
            if (unique.ContainsKey(key))
            {
                continue;
            }

            unique[key] = draft with { Text = cleanedText };
        }

        return unique.Values.ToArray();
    }

    public static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = MultiSpaceRegex().Replace(text.Trim(), " ");
        normalized = UrlRegex().Replace(normalized, " ");
        return MultiSpaceRegex().Replace(normalized, " ").Trim();
    }

    public static bool IsSpamOrBot(SocialCommentDraft draft)
    {
        if (draft.IsLikelyBot)
        {
            return true;
        }

        var text = draft.Text?.Trim() ?? string.Empty;
        if (text.Length < 2)
        {
            return true;
        }

        var lower = text.ToLowerInvariant();
        if (SpamSignals.Any(signal => lower.Contains(signal, StringComparison.Ordinal)))
        {
            return true;
        }

        if (RepeatedCharRegex.IsMatch(text) || MostlySymbolsRegex.IsMatch(text))
        {
            return true;
        }

        var handle = draft.AuthorHandle?.ToLowerInvariant() ?? string.Empty;
        if (
            handle.Contains("bot", StringComparison.Ordinal)
            || handle.Contains("spam", StringComparison.Ordinal)
            || Regex.IsMatch(handle, @"user\d{6,}")
        )
        {
            return true;
        }

        return false;
    }

    private static string BuildDedupKey(SocialCommentDraft draft, string cleanedText)
    {
        if (!string.IsNullOrWhiteSpace(draft.Id))
        {
            return $"{draft.Platform}|id:{draft.Id}";
        }

        return Normalize(
            $"{draft.Platform}|{draft.CommentUrl}|{draft.PostUrl}|{draft.AuthorHandle}|{cleanedText}"
        );
    }

    private static string Normalize(string value) =>
        value
            .Normalize(NormalizationForm.FormD)
            .Where(c =>
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark
            )
            .Aggregate(new StringBuilder(), (b, c) => b.Append(char.ToLowerInvariant(c)))
            .ToString();

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();
}
