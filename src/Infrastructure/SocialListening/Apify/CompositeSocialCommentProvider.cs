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
        var liveResult = await apifyProvider.FetchAsync(
            platform,
            refresh,
            cancellationToken,
            topic,
            sourceUrls
        );
        if (liveResult.Comments.Count > 0)
        {
            return liveResult;
        }

        var hasConfiguredSources =
            sourceUrls?.Values.Any(url => !string.IsNullOrWhiteSpace(url)) == true;

        // Búsqueda del Observatorio por tema/territorio sin URLs: usar demo filtrado.
        if (!hasConfiguredSources)
        {
            var demoResult = await demoProvider.FetchAsync(
                platform,
                refresh,
                cancellationToken,
                topic,
                sourceUrls
            );
            return SocialCommentFetchResult.Demo(
                demoResult.Comments,
                liveResult.Message
                    ?? "No hay fuentes en vivo configuradas. Se muestran datos demostrativos del Observatorio Territorial."
            );
        }

        // Vacío intencional (URL incorrecta, token faltante, etc.): no sustituir con demo.
        if (liveResult.DataSource == "live" || !string.IsNullOrWhiteSpace(liveResult.Message))
        {
            return liveResult;
        }

        var fallbackDemo = await demoProvider.FetchAsync(
            platform,
            refresh,
            cancellationToken,
            topic,
            sourceUrls
        );
        return SocialCommentFetchResult.Demo(
            fallbackDemo.Comments,
            liveResult.Message
                ?? "No se obtuvieron comentarios en vivo. Se muestran datos demostrativos."
        );
    }
}
