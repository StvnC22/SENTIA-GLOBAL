using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AnalisisSentimiento.Application.SocialListening.Geography;

public static class TerritorialLocationResolver
{
    public static TerritorialLocation Resolve(params string?[] fragments)
    {
        var haystack = Normalize(string.Join(' ', fragments.Where(value => !string.IsNullOrWhiteSpace(value))));
        if (haystack.Length == 0)
        {
            return TerritorialLocation.Unknown;
        }

        var source = InferSource(fragments);
        foreach (var place in EcuadorTerritoryCatalog.Places)
        {
            foreach (var alias in place.Aliases)
            {
                if (!ContainsPlace(haystack, Normalize(alias)))
                {
                    continue;
                }

                return new TerritorialLocation(
                    place.Province,
                    place.Canton,
                    place.Parish,
                    source,
                    FormatDisplayName(place.Province, place.Canton, place.Parish)
                );
            }
        }

        return TerritorialLocation.Unknown;
    }

    private static string InferSource(IReadOnlyList<string?> fragments)
    {
        if (!string.IsNullOrWhiteSpace(fragments.ElementAtOrDefault(0)))
        {
            return "metadata";
        }

        return fragments.Any(value => !string.IsNullOrWhiteSpace(value)) ? "text" : "none";
    }

    private static bool ContainsPlace(string haystack, string alias)
    {
        if (alias.Length < 4)
        {
            return false;
        }

        var pattern = $@"\b{Regex.Escape(alias)}\b";
        return Regex.IsMatch(haystack, pattern, RegexOptions.CultureInvariant);
    }

    private static string FormatDisplayName(string province, string? canton, string? parish)
    {
        var parts = new[] { parish, canton, province }.Where(part => !string.IsNullOrWhiteSpace(part));
        return string.Join(", ", parts);
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
