using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Infrastructure.SocialListening;

namespace AnalisisSentimiento.Infrastructure.SocialListening.Apify;

public sealed class CompositeSocialCommentProvider(
    ApifySocialCommentProvider apifyProvider,
    OfficialXSocialCommentProvider officialXProvider,
    OfficialMetaSocialCommentProvider officialMetaProvider,
    DemoSocialCommentProvider demoProvider
) : ISocialCommentProvider
{
    public async Task<SocialCommentFetchResult> FetchAsync(
        string? platform,
        bool refresh,
        CancellationToken cancellationToken,
        string? topic = null,
        IReadOnlyDictionary<string, string>? sourceUrls = null,
        string? search = null
    )
    {
        if (
            platform is not null
            && officialMetaProvider.IsConfiguredFor(platform)
            && (
                string.Equals(platform, "facebook", StringComparison.OrdinalIgnoreCase)
                || string.Equals(platform, "instagram", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return await officialMetaProvider.FetchAsync(
                platform,
                refresh,
                topic,
                search,
                sourceUrls?.GetValueOrDefault(platform),
                cancellationToken
            );
        }

        if (
            string.Equals(platform, "x", StringComparison.OrdinalIgnoreCase)
            && officialXProvider.IsConfigured
        )
        {
            return await officialXProvider.FetchAsync(
                refresh,
                topic,
                search,
                sourceUrls?.GetValueOrDefault("x"),
                cancellationToken
            );
        }

        if (
            string.IsNullOrWhiteSpace(platform)
            && (officialXProvider.IsConfigured || officialMetaProvider.IsConfigured)
        )
        {
            var results = new List<SocialCommentFetchResult>();
            foreach (var network in new[] { "instagram", "facebook", "tiktok" })
            {
                results.Add(
                    officialMetaProvider.IsConfiguredFor(network)
                        ? await officialMetaProvider.FetchAsync(
                            network,
                            refresh,
                            topic,
                            search,
                            sourceUrls?.GetValueOrDefault(network),
                            cancellationToken
                        )
                        : await apifyProvider.FetchAsync(
                            network,
                            refresh,
                            cancellationToken,
                            topic,
                            sourceUrls,
                            search
                        )
                );
            }
            results.Add(
                officialXProvider.IsConfigured
                    ? await officialXProvider.FetchAsync(
                        refresh,
                        topic,
                        search,
                        sourceUrls?.GetValueOrDefault("x"),
                        cancellationToken
                    )
                    : await apifyProvider.FetchAsync(
                        "x",
                        refresh,
                        cancellationToken,
                        topic,
                        sourceUrls,
                        search
                    )
            );
            return new SocialCommentFetchResult(
                "live",
                results.SelectMany(result => result.Comments).ToArray(),
                string.Join(
                    " · ",
                    results
                        .Select(result => result.Message)
                        .Where(message => !string.IsNullOrWhiteSpace(message))
                )
            );
        }

        var liveResult = await apifyProvider.FetchAsync(
            platform,
            refresh,
            cancellationToken,
            topic,
            sourceUrls,
            search
        );
        if (liveResult.Comments.Count > 0)
        {
            return liveResult;
        }

        // Vacío intencional (sin URLs), URL incorrecta u otro aviso explícito:
        // no sustituir con datos demostrativos.
        if (liveResult.DataSource == "live" || !string.IsNullOrWhiteSpace(liveResult.Message))
        {
            return liveResult;
        }

        var demoResult = await demoProvider.FetchAsync(
            platform,
            refresh,
            cancellationToken,
            topic,
            sourceUrls,
            search
        );
        var message =
            liveResult.Message
            ?? "No se obtuvieron comentarios en vivo. Se muestran datos demostrativos.";

        return SocialCommentFetchResult.Demo(demoResult.Comments, message);
    }
}
