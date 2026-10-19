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
                : PageIds.Length > 0
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

        var query = string.Join(
            ' ',
            new[] { search, topic }.Where(value => !string.IsNullOrWhiteSpace(value))
        );
        var cacheKey = $"official-meta:{platform}:{string.Join(',', ids)}:{query}";
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
                    await FetchInstagramCommentsAsync(id, query, comments, cancellationToken);
                else
                    await FetchFacebookCommentsAsync(id, query, comments, cancellationToken);
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
        string query,
        List<SocialCommentDraft> comments,
        CancellationToken cancellationToken
    )
    {
        var fields =
            "id,message,created_time,permalink_url,comments.limit(100){id,message,from,created_time,permalink_url,like_count}";
        using var document = await GetAsync(
            $"{Version}/"
                + Uri.EscapeDataString(pageId)
                + $"/posts?fields={Uri.EscapeDataString(fields)}&limit=30",
            cancellationToken
        );
        if (
            !document.RootElement.TryGetProperty("data", out var posts)
            || posts.ValueKind != JsonValueKind.Array
        )
            return;

        foreach (var post in posts.EnumerateArray())
        {
            var postText = GetString(post, "message") ?? string.Empty;
            if (!MatchesQuery(postText, query))
                continue;
            var postUrl = GetString(post, "permalink_url") ?? string.Empty;
            if (
                !post.TryGetProperty("comments", out var container)
                || !container.TryGetProperty("data", out var values)
                || values.ValueKind != JsonValueKind.Array
            )
                continue;
            foreach (var item in values.EnumerateArray())
            {
                var draft = MapComment(item, "facebook", postUrl);
                if (draft is not null && MatchesQuery(draft.Text, query))
                    comments.Add(draft);
            }
        }
    }

    private async Task FetchInstagramCommentsAsync(
        string accountId,
        string query,
        List<SocialCommentDraft> comments,
        CancellationToken cancellationToken
    )
    {
        using var mediaDocument = await GetAsync(
            $"{Version}/"
                + Uri.EscapeDataString(accountId)
                + "/media?fields=id,caption,permalink,timestamp,comments_count,like_count&limit=30",
            cancellationToken
        );
        if (
            !mediaDocument.RootElement.TryGetProperty("data", out var media)
            || media.ValueKind != JsonValueKind.Array
        )
            return;

        foreach (var post in media.EnumerateArray())
        {
            var postText = GetString(post, "caption") ?? string.Empty;
            if (!MatchesQuery(postText, query))
                continue;
            var mediaId = GetString(post, "id");
            if (string.IsNullOrWhiteSpace(mediaId))
                continue;
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
            var postUrl = GetString(post, "permalink") ?? string.Empty;
            foreach (var item in values.EnumerateArray())
            {
                var draft = MapComment(item, "instagram", postUrl);
                if (draft is not null && MatchesQuery(draft.Text, query))
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

    private static SocialCommentDraft? MapComment(JsonElement item, string platform, string postUrl)
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
            Reactions: reactions
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

    private static bool MatchesQuery(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;
        var clauses = query.Split(
            " OR ",
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );
        return clauses.Any(clause =>
        {
            var tokens = clause.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            var excluded = false;
            foreach (var token in tokens)
            {
                if (token.Equals("AND", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (token.Equals("NOT", StringComparison.OrdinalIgnoreCase))
                {
                    excluded = true;
                    continue;
                }
                var term = token.Trim('"', '(', ')', '#');
                var contains = text.Contains(term, StringComparison.OrdinalIgnoreCase);
                if (excluded == contains)
                    return false;
                excluded = false;
            }
            return true;
        });
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
