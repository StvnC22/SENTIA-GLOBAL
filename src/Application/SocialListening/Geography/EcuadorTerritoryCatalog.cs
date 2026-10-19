namespace AnalisisSentimiento.Application.SocialListening.Geography;

internal static class EcuadorTerritoryCatalog
{
    internal sealed record Place(
        string Province,
        string? Canton,
        string? Parish,
        IReadOnlyList<string> Aliases
    );

    public static IReadOnlyList<Place> Places { get; } = Build();

    private static IReadOnlyList<Place> Build()
    {
        var places = new List<Place>
        {
            Parish("Manabí", "Manta", "Tarqui", "tarqui"),
            Parish("Manabí", "Manta", "Eloy Alfaro", "eloy alfaro"),
            Parish("Manabí", "Manta", "San Mateo", "san mateo"),
            Parish("Manabí", "Manta", "Los Esteros", "los esteros"),
            Parish("Manabí", "Manta", "Santa Marianita", "santa marianita"),
            Parish("Manabí", "Portoviejo", "Colón", "colon", "colón"),
            Parish("Manabí", "Portoviejo", "Picoazá", "picoaza", "picoazá"),
            Parish("Manabí", "Montecristi", "La Pila", "la pila"),
            Canton("Manabí", "Manta", "manta"),
            Canton("Manabí", "Portoviejo", "portoviejo"),
            Canton("Manabí", "Montecristi", "montecristi"),
            Canton("Manabí", "Jaramijó", "jaramijo", "jaramijó"),
            Canton("Manabí", "Chone", "chone"),
            Canton("Manabí", "El Carmen", "el carmen"),
            Canton("Manabí", "Jipijapa", "jipijapa"),
            Canton("Manabí", "Pedernales", "pedernales"),
            Canton("Manabí", "Bahía de Caráquez", "bahia de caraquez", "bahía de caráquez", "bahia"),
            Canton("Manabí", "Sucre", "sucre manabi", "sucre manabí"),
            Canton("Manabí", "Rocafuerte", "rocafuerte"),
            Canton("Manabí", "Tosagua", "tosagua"),
            Canton("Manabí", "San Vicente", "san vicente"),
            Canton("Manabí", "Jama", "jama"),
            Canton("Manabí", "Puerto López", "puerto lopez", "puerto lópez"),
            Canton("Manabí", "Santa Ana", "santa ana"),
            Canton("Manabí", "Calceta", "calceta"),
            Canton("Manabí", "Bolívar", "bolivar manabi", "bolívar manabí"),
            Canton("Guayas", "Guayaquil", "guayaquil"),
            Canton("Guayas", "Durán", "duran", "durán"),
            Canton("Guayas", "Milagro", "milagro"),
            Canton("Pichincha", "Quito", "quito"),
            Canton("Azuay", "Cuenca", "cuenca"),
            Canton("El Oro", "Machala", "machala"),
            Canton("Tungurahua", "Ambato", "ambato"),
            Canton("Santo Domingo de los Tsáchilas", "Santo Domingo", "santo domingo"),
            Canton("Esmeraldas", "Esmeraldas", "esmeraldas"),
            Canton("Imbabura", "Ibarra", "ibarra"),
            Canton("Loja", "Loja", "loja"),
            Canton("Chimborazo", "Riobamba", "riobamba"),
            Canton("Los Ríos", "Quevedo", "quevedo"),
            Canton("Los Ríos", "Babahoyo", "babahoyo"),
            Canton("Cotopaxi", "Latacunga", "latacunga"),
            Province("Manabí", "manabi", "manabí"),
            Province("Guayas", "guayas"),
            Province("Pichincha", "pichincha"),
            Province("Azuay", "azuay"),
            Province("El Oro", "el oro"),
            Province("Esmeraldas", "provincia esmeraldas"),
            Province("Los Ríos", "los rios", "los ríos"),
            Province("Loja", "provincia loja"),
            Province("Tungurahua", "tungurahua"),
            Province("Chimborazo", "chimborazo"),
            Province("Imbabura", "imbabura"),
            Province("Cotopaxi", "cotopaxi"),
            Province("Santo Domingo de los Tsáchilas", "tsachilas", "tsáchilas"),
            Province("Santa Elena", "santa elena"),
            Province("Orellana", "orellana"),
            Province("Napo", "napo"),
            Province("Pastaza", "pastaza"),
            Province("Morona Santiago", "morona santiago"),
            Province("Zamora Chinchipe", "zamora chinchipe"),
            Province("Sucumbíos", "sucumbios", "sucumbíos"),
            Province("Carchi", "carchi"),
            Province("Cañar", "canar", "cañar"),
            Province("Bolívar", "bolivar", "bolívar"),
            Province("Galápagos", "galapagos", "galápagos"),
        };

        return places
            .OrderByDescending(place => place.Parish is null ? 0 : 2)
            .ThenByDescending(place => place.Canton is null ? 0 : 1)
            .ThenByDescending(place => place.Aliases.Max(alias => alias.Length))
            .ToArray();
    }

    private static Place Parish(string province, string canton, string parish, params string[] aliases) =>
        new(province, canton, parish, aliases.Concat([parish]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

    private static Place Canton(string province, string canton, params string[] aliases) =>
        new(province, canton, null, aliases.Concat([canton]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

    private static Place Province(string province, params string[] aliases) =>
        new(province, null, null, aliases.Concat([province]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
}
