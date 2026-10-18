using System.Text;
using System.Text.RegularExpressions;

namespace AnalisisSentimiento.Application.SocialListening.Search;

public static partial class BooleanQueryMatcher
{
    public static bool Matches(string? query, string haystack)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var normalizedHaystack = Normalize(haystack);
        var expression = NormalizeOperators(query.Trim());
        return Evaluate(expression, normalizedHaystack);
    }

    private static bool Evaluate(string expression, string haystack)
    {
        expression = expression.Trim();
        if (expression.Length == 0)
        {
            return true;
        }

        while (
            expression.StartsWith('(') && FindMatchingParen(expression, 0) == expression.Length - 1
        )
        {
            expression = expression[1..^1].Trim();
        }

        var orParts = SplitTopLevel(expression, " OR ");
        if (orParts.Count > 1)
        {
            return orParts.Any(part => Evaluate(part, haystack));
        }

        var andParts = SplitTopLevel(expression, " AND ");
        if (andParts.Count > 1)
        {
            return andParts.All(part => Evaluate(part, haystack));
        }

        // "ULEAM NOT spam" ≡ ULEAM AND NOT spam
        var notParts = SplitTopLevel(expression, " NOT ");
        if (notParts.Count > 1)
        {
            if (!Evaluate(notParts[0], haystack))
            {
                return false;
            }

            return notParts.Skip(1).All(part => !Evaluate(part, haystack));
        }

        if (expression.StartsWith("NOT ", StringComparison.Ordinal))
        {
            return !Evaluate(expression[4..].Trim(), haystack);
        }

        return MatchTerm(expression, haystack);
    }

    private static bool MatchTerm(string term, string haystack)
    {
        term = term.Trim().Trim('"');
        if (term.Length == 0)
        {
            return true;
        }

        // Sinónimos: palabra~sinonimo1,sinonimo2
        if (term.Contains('~', StringComparison.Ordinal))
        {
            var pieces = term.Split('~', 2, StringSplitOptions.TrimEntries);
            var variants = new List<string> { pieces[0] };
            if (pieces.Length > 1)
            {
                variants.AddRange(
                    pieces[1]
                        .Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                        )
                );
            }

            return variants.Any(variant => MatchSingleToken(variant, haystack));
        }

        // Sinónimos cortos con | : uleam|universidad
        if (term.Contains('|', StringComparison.Ordinal))
        {
            return term.Split(
                    '|',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
                .Any(variant => MatchSingleToken(variant, haystack));
        }

        return MatchSingleToken(term, haystack);
    }

    private static bool MatchSingleToken(string token, string haystack)
    {
        var normalized = Normalize(token);
        if (normalized.Length == 0)
        {
            return true;
        }

        if (normalized.StartsWith('#'))
        {
            return haystack.Contains(normalized, StringComparison.Ordinal)
                || haystack.Contains(normalized.TrimStart('#'), StringComparison.Ordinal);
        }

        return haystack.Contains(normalized, StringComparison.Ordinal);
    }

    private static string NormalizeOperators(string query)
    {
        var result = query;
        result = SpanishNotRegex().Replace(result, " NOT ");
        result = SpanishAndRegex().Replace(result, " AND ");
        result = SpanishOrRegex().Replace(result, " OR ");
        result = MultiSpaceRegex().Replace(result, " ").Trim();
        return result;
    }

    private static List<string> SplitTopLevel(string expression, string separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var buffer = new StringBuilder();
        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];
            if (c == '(')
                depth++;
            if (c == ')')
                depth = Math.Max(0, depth - 1);

            if (
                depth == 0
                && i + separator.Length <= expression.Length
                && string.Compare(
                    expression,
                    i,
                    separator,
                    0,
                    separator.Length,
                    StringComparison.OrdinalIgnoreCase
                ) == 0
            )
            {
                parts.Add(buffer.ToString().Trim());
                buffer.Clear();
                i += separator.Length - 1;
                continue;
            }

            buffer.Append(c);
        }

        parts.Add(buffer.ToString().Trim());
        return parts.Where(p => p.Length > 0).ToList();
    }

    private static int FindMatchingParen(string expression, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < expression.Length; i++)
        {
            if (expression[i] == '(')
                depth++;
            if (expression[i] == ')')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
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

    [GeneratedRegex(@"\bNO\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpanishNotRegex();

    [GeneratedRegex(@"\bY\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpanishAndRegex();

    [GeneratedRegex(@"\bO\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpanishOrRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();
}
