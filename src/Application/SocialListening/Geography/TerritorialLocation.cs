namespace AnalisisSentimiento.Application.SocialListening.Geography;

public sealed record TerritorialLocation(
    string? Province,
    string? Canton,
    string? Parish,
    string Source,
    string DisplayName
)
{
    public static TerritorialLocation Unknown { get; } =
        new(null, null, null, "none", "Sin ubicar");

    public bool IsLocated => !string.IsNullOrWhiteSpace(Province);
}
