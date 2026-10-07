namespace AnalisisSentimiento.Application.Common.Interfaces;

public interface IApiCredentialStore
{
    Task<ApiCredentials> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(ApiCredentials credentials, CancellationToken cancellationToken);
}

public sealed record ApiCredentials(
    string? Instagram,
    string? Facebook,
    string? TikTok,
    string? X,
    string? ArtificialIntelligence,
    DateTimeOffset? UpdatedAt
)
{
    public static ApiCredentials Empty { get; } = new(null, null, null, null, null, null);
}
