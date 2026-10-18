namespace AnalisisSentimiento.Application.SocialListening.Territory;

public static class ManabiTerritoryCatalog
{
    public const string Province = "Manabí";

    public static IReadOnlyList<CantonInfo> Cantons { get; } =
    [
        new("Portoviejo", ["Portoviejo", "Abdón Calderón", "Alajuela", "Chirijos", "San Plácido"]),
        new("Manta", ["Manta", "San Mateo", "Santa Marianita", "San Lorenzo"]),
        new(
            "Chone",
            [
                "Chone",
                "Boyacá",
                "Canuto",
                "Convento",
                "Chibunga",
                "Eloy Alfaro",
                "Ricaurte",
                "San Antonio",
            ]
        ),
        new("Bahía de Caráquez", ["Bahía de Caráquez", "Leónidas Plaza Gutiérrez", "San Isidro"]),
        new(
            "Jipijapa",
            [
                "Jipijapa",
                "América",
                "El Anegado",
                "Julcuy",
                "La Unión",
                "Membrillal",
                "Pedro Pablo Gómez",
                "Puerto Cayo",
            ]
        ),
        new("Montecristi", ["Montecristi", "Anibal San Andrés", "General Alfaro", "La Pila"]),
        new("El Carmen", ["El Carmen", "4 de Diciembre", "Wilfrido Loor Moreira"]),
        new("Pedernales", ["Pedernales", "Cojimíes", "10 de Agosto", "Atahualpa"]),
        new("Rocafuerte", ["Rocafuerte"]),
        new("Santa Ana", ["Santa Ana", "Ayacucho", "Honorato Vásquez", "La Unión", "San Pablo"]),
        new("Tosagua", ["Tosagua", "Bachillero", "Ángel Pedro Giler"]),
        new("Calceta", ["Calceta", "Quiroga", "Membrillo"]),
        new("Junín", ["Junín"]),
        new("Sucre", ["Bahía de Caráquez", "Charapotó", "San Isidro"]),
        new("Paján", ["Paján", "Campozano", "Cascol", "Guale", "Lascano"]),
        new("Flavio Alfaro", ["Flavio Alfaro", "San Francisco de Novillo", "Zapallo"]),
        new("Jama", ["Jama"]),
        new("San Vicente", ["San Vicente", "Canoa"]),
        new("Olmedo", ["Olmedo"]),
        new("Puerto López", ["Puerto López", "Machalilla", "Salango"]),
        new("24 de Mayo", ["Sucre", "Bellavista", "Noboa", "Arq. Sixto Durán Ballén"]),
        new("Bolívar", ["Calceta", "Membrillo", "Quiroga"]),
    ];

    public static IReadOnlyList<string> CantonNames =>
        Cantons
            .Select(c => c.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToArray();

    public static IReadOnlyList<string> ParishesFor(string? canton)
    {
        if (string.IsNullOrWhiteSpace(canton))
        {
            return Cantons
                .SelectMany(c => c.Parishes)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToArray();
        }

        return Cantons
            .Where(c => c.Name.Equals(canton, StringComparison.OrdinalIgnoreCase))
            .SelectMany(c => c.Parishes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToArray();
    }

    public static TerritoryMatch? InferFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = Normalize(text);
        foreach (var canton in Cantons)
        {
            foreach (var parish in canton.Parishes)
            {
                if (normalized.Contains(Normalize(parish), StringComparison.Ordinal))
                {
                    return new TerritoryMatch(Province, canton.Name, parish);
                }
            }

            if (normalized.Contains(Normalize(canton.Name), StringComparison.Ordinal))
            {
                return new TerritoryMatch(Province, canton.Name, canton.Parishes[0]);
            }
        }

        if (normalized.Contains("manabi") || normalized.Contains("manabí"))
        {
            return new TerritoryMatch(Province, null, null);
        }

        return null;
    }

    public static bool MatchesFilter(
        string? province,
        string? canton,
        string? parish,
        string? filterProvince,
        string? filterCanton,
        string? filterParish
    )
    {
        if (
            !string.IsNullOrWhiteSpace(filterProvince)
            && !string.Equals(province, filterProvince, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Province, filterProvince, StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        if (
            !string.IsNullOrWhiteSpace(filterCanton)
            && !string.Equals(canton, filterCanton, StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        if (
            !string.IsNullOrWhiteSpace(filterParish)
            && !string.Equals(parish, filterParish, StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        return true;
    }

    private static string Normalize(string value) =>
        value
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c =>
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark
            )
            .Aggregate(
                new System.Text.StringBuilder(),
                (b, c) => b.Append(char.ToLowerInvariant(c))
            )
            .ToString();
}

public sealed record CantonInfo(string Name, IReadOnlyList<string> Parishes);

public sealed record TerritoryMatch(string Province, string? Canton, string? Parish);
