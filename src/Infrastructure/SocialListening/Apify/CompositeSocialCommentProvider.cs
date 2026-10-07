using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Infrastructure.SocialListening.Apify;

public sealed class CompositeSocialCommentProvider(
    ApifySocialCommentProvider apifyProvider,
    DemoSocialCommentProvider demoProvider
) : ISocialCommentProvider
{
    public async Task<SocialCommentFetchResult> FetchAsync(
        string? platform,
        bool refresh,
        CancellationToken cancellationToken,
        string? topic = null,
        IReadOnlyDictionary<string, string>? sourceUrls = null
    )
    {
        var liveResult = await apifyProvider.FetchAsync(platform, refresh, cancellationToken, topic, sourceUrls);
        if (liveResult.Comments.Count > 0)
        {
            return liveResult;
        }

        // Vacío intencional (sin URLs), URL incorrecta u otro aviso explícito:
        // no sustituir con datos demostrativos.
        if (
            liveResult.DataSource == "live"
            || !string.IsNullOrWhiteSpace(liveResult.Message)
        )
        {
            return liveResult;
        }

        var demoResult = await demoProvider.FetchAsync(platform, refresh, cancellationToken, topic, sourceUrls);
        var message =
            liveResult.Message
            ?? "No se obtuvieron comentarios en vivo. Se muestran datos demostrativos.";

        return SocialCommentFetchResult.Demo(demoResult.Comments, message);
    }
}
