using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.ApiCredentials.Queries.GetApiCredentialStatus;

public record GetApiCredentialStatusQuery : IQuery<ApiCredentialStatus>;

internal class GetApiCredentialStatusQueryHandler(IApiCredentialStore credentialStore)
    : IRequestHandler<GetApiCredentialStatusQuery, ApiCredentialStatus>
{
    public async Task<ApiCredentialStatus> Handle(
        GetApiCredentialStatusQuery request,
        CancellationToken cancellationToken
    )
    {
        var credentials = await credentialStore.GetAsync(cancellationToken);
        return ApiCredentialStatus.From(credentials);
    }
}

public sealed record ApiCredentialStatus(
    bool Instagram,
    bool Facebook,
    bool TikTok,
    bool X,
    bool ArtificialIntelligence,
    DateTimeOffset? UpdatedAt
)
{
    public static ApiCredentialStatus From(Common.Interfaces.ApiCredentials credentials) =>
        new(
            HasValue(credentials.Instagram),
            HasValue(credentials.Facebook),
            HasValue(credentials.TikTok),
            HasValue(credentials.X),
            HasValue(credentials.ArtificialIntelligence),
            credentials.UpdatedAt
        );

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
