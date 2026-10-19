using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Infrastructure.SocialListening.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.SocialListening;

public sealed partial class OfficialTikTokSocialCommentProvider(
    HttpClient httpClient,
    IOptions<SocialListeningOptions> options,
    IMemoryCache cache,
    TimeProvider timeProvider
)
{
    private OfficialSocialApiOptions Api => options.Value.OfficialApis;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Api.TikTokResearchClientKey)
        && !string.IsNullOrWhiteSpace(Api.TikTokResearchClientSecret);

    public async Task<SocialCommentFetchResult> FetchAsync(
        bool refresh,
        string? topic,
        string? search,
        string? sourceUrl,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken
    )
    {
        if (!IsConfigured)
        {
            return new SocialCommentFetchResult(
                "live",
                [],
                "Configura las credenciales de un proyecto aprobado de TikTok Research API para consultar TikTok oficialmente."
            );
        }

        var expression = string.Join(
            " ",
            new[] { search, topic }.Where(value => !string.IsNullOrWhiteSpace(value))
        );
        var conditions = BuildKeywordConditions(expression);
        if (conditions.Count == 0 && ExtractUsername(sourceUrl) is { } sourceUsername)
        {
            conditions.Add(
                new
                {
                    operation = "IN",
                    field_name = "username",
                    field_values = new[] { sourceUsername },
                }
            );
        }
        if (conditions.Count == 0)
        {
            return new SocialCommentFetchResult(
                "live",
                [],
                "TikTok Research API requiere una palabra clave, hashtag o cuenta para iniciar la consulta."
            );
        }

        var today = timeProvider.GetUtcNow().UtcDateTime.Date;
        var endDate = to?.ToUniversalTime().Date ?? today;
        if (endDate > today)
            endDate = today;
        var startDate = from?.ToUniversalTime().Date ?? endDate.AddDays(-29);
        if (endDate < startDate)
        {
            return new SocialCommentFetchResult(
                "live",
                [],
                "El rango de fechas de TikTok termina antes de su fecha inicial."
            );
        }

        var maxPosts = Math.Clamp(options.Value.TikTokMaxPosts, 1, 100);
        var maxComments = Math.Clamp(options.Value.MaxCommentsPerPost, 0, 100);
        var maxTotal = Math.Clamp(options.Value.MaxCommentsPerPlatform, 10, 1000);
        var cacheKey =
            $"official-tiktok:{expression}:{string.Join(';', conditions.Select(condition => JsonSerializer.Serialize(condition)))}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
        if (
            !refresh
            && cache.TryGetValue(cacheKey, out SocialCommentFetchResult? cached)
            && cached is not null
        )
            return cached;

        try
        {
            var accessToken = await GetAccessTokenAsync(cancellationToken);
            var videos = new List<JsonElement>();
            for (var windowStart = startDate; windowStart <= endDate && videos.Count < maxPosts; )
            {
                var windowEnd =
                    windowStart.AddDays(29) < endDate ? windowStart.AddDays(29) : endDate;
                await QueryVideosAsync(
                    conditions,
                    windowStart,
                    windowEnd,
                    maxPosts - videos.Count,
                    accessToken,
                    videos,
                    cancellationToken
                );
                windowStart = windowEnd.AddDays(1);
            }

            var drafts = new List<SocialCommentDraft>();
            foreach (var video in videos)
            {
                var videoId = ReadScalar(video, "id");
                if (string.IsNullOrWhiteSpace(videoId))
                    continue;
                var username = GetString(video, "username") ?? "usuario";
                var videoUrl =
                    $"https://www.tiktok.com/@{Uri.EscapeDataString(username)}/video/{videoId}";
                var text = GetString(video, "video_description") ?? string.Empty;
                var title = text.Length > 180 ? $"{text[..177]}..." : text;
                var publishedAt = GetUnixTime(video, "create_time");
                drafts.Add(
                    new SocialCommentDraft(
                        $"tiktok-video:{videoId}",
                        "tiktok",
                        username,
                        $"@{username}",
                        $"https://www.tiktok.com/@{Uri.EscapeDataString(username)}",
                        text,
                        videoUrl,
                        title,
                        videoUrl,
                        publishedAt,
                        Reactions: GetInt(video, "like_count"),
                        RegionCode: GetString(video, "region_code"),
                        ContentType: "post"
                    )
                );

                if (maxComments == 0 || drafts.Count >= maxTotal)
                    continue;
                await QueryCommentsAsync(
                    videoId,
                    videoUrl,
                    maxComments,
                    accessToken,
                    drafts,
                    maxTotal,
                    cancellationToken
                );
            }

            var result = new SocialCommentFetchResult(
                "live",
                drafts.Take(maxTotal).ToArray(),
                "Fuente oficial TikTok Research API. Requiere aprobación de TikTok Research Tools; los datos incluyen el código de región de registro de la cuenta, no una ubicación precisa de la publicación."
            );
            cache.Set(cacheKey, result, TimeSpan.FromMinutes(options.Value.CacheMinutes));
            return result;
        }
        catch (HttpRequestException exception)
        {
            var status = exception.StatusCode is null
                ? "sin respuesta"
                : $"HTTP {(int)exception.StatusCode}";
            return new SocialCommentFetchResult(
                "live",
                [],
                $"TikTok Research API rechazó la consulta ({status}). Verifica la aprobación del proyecto, el scope research.data.basic y sus credenciales."
            );
        }
        catch (JsonException)
        {
            return new SocialCommentFetchResult(
                "live",
                [],
                "TikTok Research API devolvió una respuesta que no se pudo interpretar."
            );
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "official-tiktok:research-access-token";
        if (
            cache.TryGetValue(cacheKey, out string? cachedToken)
            && !string.IsNullOrWhiteSpace(cachedToken)
        )
            return cachedToken;

        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token/")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_key"] = Api.TikTokResearchClientKey,
                    ["client_secret"] = Api.TikTokResearchClientSecret,
                    ["grant_type"] = "client_credentials",
                }
            ),
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                "No se pudo obtener un token de TikTok Research API.",
                null,
                response.StatusCode
            );
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken
        );
        var token = GetString(document.RootElement, "access_token");
        if (string.IsNullOrWhiteSpace(token))
            throw new JsonException("La respuesta de TikTok no incluyó access_token.");
        var expiresIn = GetInt(document.RootElement, "expires_in") ?? 7200;
        cache.Set(cacheKey, token, TimeSpan.FromSeconds(Math.Max(60, expiresIn - 300)));
        return token;
    }

    private async Task QueryVideosAsync(
        IReadOnlyList<object> conditions,
        DateTime startDate,
        DateTime endDate,
        int remaining,
        string accessToken,
        List<JsonElement> videos,
        CancellationToken cancellationToken
    )
    {
        const string fields =
            "id,video_description,create_time,region_code,share_count,view_count,like_count,comment_count,username,hashtag_names";
        long cursor = 0;
        var maxResults = Math.Min(100, remaining);
        do
        {
            var body = JsonSerializer.SerializeToElement(
                new
                {
                    query = new { or = conditions },
                    start_date = startDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                    end_date = endDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                    max_count = maxResults,
                    cursor,
                }
            );
            using var document = await PostAsync(
                $"research/video/query/?fields={Uri.EscapeDataString(fields)}",
                body,
                accessToken,
                cancellationToken
            );
            if (!document.RootElement.TryGetProperty("data", out var data))
                break;
            if (
                data.TryGetProperty("videos", out var items)
                && items.ValueKind == JsonValueKind.Array
            )
            {
                videos.AddRange(
                    items
                        .EnumerateArray()
                        .Take(Math.Max(0, remaining - videos.Count))
                        .Select(item => item.Clone())
                );
            }
            var hasMore =
                data.TryGetProperty("has_more", out var more)
                && more.ValueKind == JsonValueKind.True;
            cursor =
                data.TryGetProperty("cursor", out var nextCursor)
                && nextCursor.TryGetInt64(out var value)
                    ? value
                    : 0;
            if (!hasMore || videos.Count >= remaining || cursor == 0)
                break;
        } while (true);
    }

    private async Task QueryCommentsAsync(
        string videoId,
        string videoUrl,
        int maxComments,
        string accessToken,
        List<SocialCommentDraft> drafts,
        int maxTotal,
        CancellationToken cancellationToken
    )
    {
        if (
            !long.TryParse(
                videoId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var numericId
            )
        )
            return;
        const string fields =
            "id,video_id,text,like_count,reply_count,parent_comment_id,create_time";
        long cursor = 0;
        var commentCount = 0;
        do
        {
            var body = JsonSerializer.SerializeToElement(
                new
                {
                    video_id = numericId,
                    max_count = Math.Min(100, maxComments - commentCount),
                    cursor,
                }
            );
            using var document = await PostAsync(
                $"research/video/comment/list/?fields={Uri.EscapeDataString(fields)}",
                body,
                accessToken,
                cancellationToken
            );
            if (!document.RootElement.TryGetProperty("data", out var data))
                break;
            var values =
                data.TryGetProperty("comments", out var items)
                && items.ValueKind == JsonValueKind.Array
                    ? items.EnumerateArray().ToArray()
                    : Array.Empty<JsonElement>();
            foreach (var item in values)
            {
                var id = ReadScalar(item, "id");
                var text = GetString(item, "text");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text))
                    continue;
                drafts.Add(
                    new SocialCommentDraft(
                        $"tiktok-comment:{id}",
                        "tiktok",
                        "Usuario no identificado",
                        "Autor no divulgado por la API",
                        videoUrl,
                        text,
                        videoUrl,
                        text.Length > 180 ? $"{text[..177]}..." : text,
                        videoUrl,
                        GetUnixTime(item, "create_time"),
                        Reactions: GetInt(item, "like_count")
                    )
                );
                commentCount++;
                if (drafts.Count >= maxTotal || commentCount >= maxComments)
                    return;
            }
            var hasMore =
                data.TryGetProperty("has_more", out var more)
                && more.ValueKind == JsonValueKind.True;
            cursor =
                data.TryGetProperty("cursor", out var nextCursor)
                && nextCursor.TryGetInt64(out var value)
                    ? value
                    : 0;
            if (!hasMore || values.Length == 0 || cursor == 0)
                break;
        } while (true);
    }

    private async Task<JsonDocument> PostAsync(
        string path,
        JsonElement body,
        string accessToken,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(
                body.GetRawText(),
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
        request.Headers.Authorization = new("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                "La consulta a TikTok Research API no tuvo éxito.",
                null,
                response.StatusCode
            );
        var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken
        );
        if (
            document.RootElement.TryGetProperty("error", out var error)
            && GetString(error, "code") is { } code
            && !code.Equals("ok", StringComparison.OrdinalIgnoreCase)
        )
        {
            document.Dispose();
            throw new HttpRequestException("TikTok Research API devolvió un error de aplicación.");
        }
        return document;
    }

    private static List<object> BuildKeywordConditions(string expression)
    {
        var terms = new List<string>();
        var excludeNext = false;
        foreach (Match match in SearchTokens().Matches(expression))
        {
            var token = match.Value;
            if (token.Equals("NOT", StringComparison.OrdinalIgnoreCase))
            {
                excludeNext = true;
                continue;
            }
            if (
                token.Equals("AND", StringComparison.OrdinalIgnoreCase)
                || token.Equals("OR", StringComparison.OrdinalIgnoreCase)
                || token is "(" or ")"
            )
                continue;
            if (excludeNext)
            {
                excludeNext = false;
                continue;
            }
            var term = token.Trim('"', '(', ')');
            if (!string.IsNullOrWhiteSpace(term))
                terms.Add(term);
        }
        var positiveTerms = terms.Distinct(StringComparer.OrdinalIgnoreCase);
        var conditions = new List<object>();
        foreach (var term in positiveTerms)
        {
            var hashtag = term.StartsWith('#');
            conditions.Add(
                new
                {
                    operation = "EQ",
                    field_name = hashtag ? "hashtag_name" : "keyword",
                    field_values = new[] { hashtag ? term[1..] : term },
                }
            );
        }
        return conditions;
    }

    private static string? ExtractUsername(string? sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
            return null;
        var match = UsernameRegex().Match(uri.AbsolutePath);
        return match.Success ? match.Groups[1].Value.TrimStart('@') : null;
    }

    private static DateTimeOffset GetUnixTime(JsonElement element, string property) =>
        GetLong(element, property) is { } unixTime
            ? DateTimeOffset.FromUnixTimeSeconds(unixTime)
            : DateTimeOffset.UnixEpoch;

    private static int? GetInt(JsonElement element, string property) =>
        GetLong(element, property) is { } value && value <= int.MaxValue && value >= int.MinValue
            ? (int)value
            : null;

    private static long? GetLong(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
            ? value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
                ? number
                : value.ValueKind == JsonValueKind.String
                && long.TryParse(value.GetString(), out number)
                    ? number
                    : null
            : null;

    private static string? ReadScalar(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            }
            : null;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    [GeneratedRegex(
        "\\\"[^\\\"]+\\\"|\\b(?:AND|OR|NOT)\\b|[()]|[^\\s()]+",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex SearchTokens();

    [GeneratedRegex("^/([^/?]+)/?$")]
    private static partial Regex UsernameRegex();
}
