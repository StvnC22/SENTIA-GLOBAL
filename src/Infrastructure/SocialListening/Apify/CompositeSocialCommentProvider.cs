using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Infrastructure.SocialListening.Apify;

public sealed class CompositeSocialCommentProvider(ApifySocialCommentProvider apifyProvider)
    : ISocialCommentProvider
{
    public Task<SocialCommentFetchResult> FetchAsync(
        string? platform,
        bool refresh,
        CancellationToken cancellationToken,
        string? topic = null,
        IReadOnlyDictionary<string, string>? sourceUrls = null
    )
    {
        return apifyProvider.FetchAsync(platform, refresh, cancellationToken, topic, sourceUrls);
    }
}
