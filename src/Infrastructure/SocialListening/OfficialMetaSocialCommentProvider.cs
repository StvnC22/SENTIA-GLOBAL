using System.Globalization;
using System.Text.Json;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Infrastructure.SocialListening.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.SocialListening;

public sealed class OfficialMetaSocialCommentProvider(
    HttpClient httpClient,
    IOptions<SocialListeningOptions> options,
    IMemoryCache cache
)
{
    private OfficialSocialApiOptions Api => options.Value.OfficialApis;

    public bool IsConfigured => IsConfiguredFor("facebook") || IsConfiguredFor("instagram");

    public bool IsConfiguredFor(string platform) =>
        !string.IsNullOrWhiteSpace(Api.MetaAccessToken)
        && (
            platform.Equals("instagram", StringComparison.OrdinalIgnoreCase)
                ? InstagramAccountIds.Length > 0
                : platform.Equals("facebook", StringComparison.OrdinalIgnoreCase)
                    && PageIds.Length > 0
        );

    private string[] PageIds => ParseIds(Api.FacebookPageIds);

    private string[] InstagramAccountIds => ParseIds(Api.InstagramBusinessAccountIds);

    public async Task<SocialCommentFetchResult> FetchAsync(
        string platform,
        bool refresh,
        string? topic,
        string? search,
        string? sourceUrl,
        CancellationToken cancellationToken
    )
    {
        var isInstagram = platform.Equals("instagram", StringComparison.OrdinalIgnoreCase);
        var ids = isInstagram ? InstagramAccountIds : PageIds;
        if (string.IsNullOrWhiteSpace(Api.MetaAccessToken) || ids.Length == 0)
        {
            return new SocialCommentFetchResult(
                "live",
                [],
                $"Configura el token autorizado y los IDs de {platform} en Aspire User Secrets."
            );
        }

        var cacheKey = $"official-meta:{platform}:{string.Join(',', ids)}";
        if (
            !refresh
            && cache.TryGetValue(cacheKey, out SocialCommentFetchResult? cached)
            && cached is not null
        )
            return cached;

        try
        {
            var comments = new List<SocialCommentDraft>();
            foreach (var id in ids)
            {
                if (isInstagram)
                    await FetchInstagramCommentsAsync(id, comments, cancellationToken);
                else
                    await FetchFacebookCommentsAsync(id, comments, cancellationToken);
            }

            var maxComments = Math.Clamp(options.Value.MaxCommentsPerPlatform, 10, 1000);
            var result = new SocialCommentFetchResult(
                "live",
                comments
                    .OrderByDescending(comment => comment.PublishedAt)
                    .Take(maxComments)
                    .ToArray(),
                isInstagram
                    ? "Fuente oficial Meta Graph API: comentarios de las cuentas profesionales autorizadas."
                    : "Fuente oficial Meta Graph API: comentarios de las páginas autorizadas."
            );
            cache.Set(cacheKey, result, TimeSpan.FromMinutes(options.Value.CacheMinutes));
            return result;
        }
        catch (HttpRequestException exception)
        {
            var statusCode = exception.StatusCode is null
                ? "sin respuesta"
                : $"HTTP {(int)exception.StatusCode}";
            return new SocialCommentFetchResult(
                "live",
                [],
                $"Meta Graph API rechazó la consulta ({statusCode}). Revisa el token, permisos y los IDs autorizados."
            );
        }
    }

    private async Task FetchFacebookCommentsAsync(
        string pageId,
        List<SocialCommentDraft> comments,
        CancellationToken cancellationToken
    )
    {
        var fields =
            "id,message,created_time,permalink_url,from{name},reactions.summary(true).limit(0),place{id,name,location{city,state,country}},comments.limit(100){id,message,from,created_time,permalink_url,like_count}";
        using var document = await GetAsync(
            $"{Version}/"
                + Uri.EscapeDataString(pageId)
                + $"/posts?fields={Uri.EscapeDataString(fields)}&limit=100",
            cancellationToken
        );
        if (
            !document.RootElement.TryGetProperty("data", out var posts)
            || posts.ValueKind != JsonValueKind.Array
        )
            return;

        foreach (var post in posts.EnumerateArray())
        {
            var postUrl = GetString(post, "permalink_url") ?? string.Empty;
            var placeName = ReadPlaceName(post);
            var postDraft = MapPost(post, "facebook", postUrl, placeName);
            if (postDraft is not null)
                comments.Add(postDraft);
            if (
                !post.TryGetProperty("comments", out var container)
                || !container.TryGetProperty("data", out var values)
                || values.ValueKind != JsonValueKind.Array
            )
                continue;
            foreach (var item in values.EnumerateArray())
            {
                var draft = MapComment(item, "facebook", postUrl, placeName);
                if (draft is not null)
                    comments.Add(draft);
            }
        }
    }

    private async Task FetchInstagramCommentsAsync(
        string accountId,
        List<SocialCommentDraft> comments,
        CancellationToken cancellationToken
    )
    {
        using var mediaDocument = await GetAsync(
            $"{Version}/"
                + Uri.EscapeDataString(accountId)
                + "/media?fields=id,caption,permalink,timestamp,comments_count,like_count,username&limit=100",
            cancellationToken
        );
        if (
            !mediaDocument.RootElement.TryGetProperty("data", out var media)
            || media.ValueKind != JsonValueKind.Array
        )
            return;

        foreach (var post in media.EnumerateArray())
        {
            var mediaId = GetString(post, "id");
            if (string.IsNullOrWhiteSpace(mediaId))
                continue;
            var postUrl = GetString(post, "permalink") ?? string.Empty;
            var postDraft = MapPost(post, "instagram", postUrl);
            if (postDraft is not null)
                comments.Add(postDraft);
            using var commentsDocument = await GetAsync(
                $"{Version}/"
                    + Uri.EscapeDataString(mediaId)
                    + "/comments?fields=id,text,username,timestamp,like_count,permalink&limit=100",
                cancellationToken
            );
            if (
                !commentsDocument.RootElement.TryGetProperty("data", out var values)
                || values.ValueKind != JsonValueKind.Array
            )
                continue;
            foreach (var item in values.EnumerateArray())
            {
                var draft = MapComment(item, "instagram", postUrl);
                if (draft is not null)
                    comments.Add(draft);
            }
        }
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new("Bearer", Api.MetaAccessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                "La consulta a Meta Graph API no tuvo éxito.",
                null,
                response.StatusCode
            );
        return await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken
        );
    }

    private string Version => Api.MetaGraphApiVersion.Trim().TrimEnd('/') + "/";

    private static SocialCommentDraft? MapComment(
        JsonElement item,
        string platform,
        string postUrl,
        string? placeName = null
    )
    {
        var id = GetString(item, "id");
        var text = GetString(item, "message") ?? GetString(item, "text");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text))
            return null;
        var from = item.TryGetProperty("from", out var author) ? GetString(author, "name") : null;
        var username = GetString(item, "username");
        var publishedAt = DateTimeOffset.TryParse(
            GetString(item, "created_time") ?? GetString(item, "timestamp"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var timestamp
        )
            ? timestamp
            : DateTimeOffset.UtcNow;
        var url = GetString(item, "permalink_url") ?? GetString(item, "permalink") ?? postUrl;
        var reactions =
            item.TryGetProperty("like_count", out var count) && count.TryGetInt32(out var parsed)
                ? parsed
                : (int?)null;
        return new SocialCommentDraft(
            id,
            platform,
            from ?? username ?? "Usuario",
            username is null ? from ?? "Usuario" : $"@{username}",
            url,
            text,
            postUrl,
            text.Length > 180 ? $"{text[..177]}..." : text,
            url,
            publishedAt,
            Reactions: reactions,
            PlaceName: placeName
        );
    }

    private static SocialCommentDraft? MapPost(
        JsonElement item,
        string platform,
        string postUrl,
        string? placeName = null
    )
    {
        var id = GetString(item, "id");
        var text = GetString(item, "message") ?? GetString(item, "caption");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text))
            return null;
        var author = item.TryGetProperty("from", out var from) ? GetString(from, "name") : null;
        var username = GetString(item, "username");
        var publishedAt = DateTimeOffset.TryParse(
            GetString(item, "created_time") ?? GetString(item, "timestamp"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var timestamp
        )
            ? timestamp
            : DateTimeOffset.UtcNow;
        var reactions = GetInteger(item, "like_count") ?? GetSummaryCount(item, "reactions");
        return new SocialCommentDraft(
            $"meta-post:{id}",
            platform,
            author ?? username ?? "Cuenta autorizada",
            username is null ? author ?? "Cuenta autorizada" : $"@{username}",
            postUrl,
            text,
            postUrl,
            text.Length > 180 ? $"{text[..177]}..." : text,
            postUrl,
            publishedAt,
            Reactions: reactions,
            PlaceName: placeName,
            ContentType: "post"
        );
    }

    private static string? ReadPlaceName(JsonElement post)
    {
        if (!post.TryGetProperty("place", out var place))
            return null;
        if (!place.TryGetProperty("location", out var location))
            return GetString(place, "name");
        return string.Join(
            ", ",
            new[]
            {
                GetString(place, "name"),
                GetString(location, "city"),
                GetString(location, "state"),
                GetString(location, "country"),
            }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase)
        );
    }

    private static string[] ParseIds(string value) =>
        value
            .Split(
                [',', ';', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInteger(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var count)
            ? count
            : null;

    private static int? GetSummaryCount(JsonElement element, string name) =>
        element.TryGetProperty(name, out var connection)
        && connection.TryGetProperty("summary", out var summary)
        && summary.TryGetProperty("total_count", out var count)
        && count.TryGetInt32(out var total)
            ? total
            : null;
}
