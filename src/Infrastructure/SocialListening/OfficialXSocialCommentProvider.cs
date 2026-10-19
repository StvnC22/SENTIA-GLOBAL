using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Infrastructure.SocialListening.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.SocialListening;

public sealed partial class OfficialXSocialCommentProvider(
    HttpClient httpClient,
    IOptions<SocialListeningOptions> options,
    IMemoryCache cache
)
{
    private const string GeoFields = "id,full_name,name,place_type,country,country_code";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.OfficialApis.XBearerToken);

    public async Task<SocialCommentFetchResult> FetchAsync(
        bool refresh,
        string? topic,
        string? search,
        string? sourceUrl,
        CancellationToken cancellationToken
    )
    {
        var query = BuildQuery(topic, search, sourceUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SocialCommentFetchResult(
                "live",
                [],
                "La API oficial de X necesita un tema, consulta o perfil para buscar."
            );
        }

        if (query.Length > 512)
        {
            return new SocialCommentFetchResult(
                "live",
                [],
                "La consulta de X supera los 512 caracteres."
            );
        }

        var cacheKey = $"official-x:{query}";
        if (
            !refresh
            && cache.TryGetValue(cacheKey, out SocialCommentFetchResult? cached)
            && cached is not null
        )
        {
            return cached;
        }

        try
        {
            var comments = await SearchAsync(query, cancellationToken);
            var result = new SocialCommentFetchResult(
                "live",
                comments,
                "Fuente oficial X API v2. Recent Search solo cubre publicaciones de los últimos siete días; la ubicación solo aparece cuando X adjunta un lugar a la publicación."
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
                $"X API rechazó la consulta ({statusCode}). Comprueba el bearer token, el nivel de acceso de la aplicación y los límites del plan."
            );
        }
    }

    private async Task<IReadOnlyList<SocialCommentDraft>> SearchAsync(
        string query,
        CancellationToken cancellationToken
    )
    {
        var comments = new List<SocialCommentDraft>();
        string? nextToken = null;
        var maxComments = Math.Clamp(options.Value.MaxCommentsPerPlatform, 10, 250);
        var maxPages = (int)Math.Ceiling(maxComments / 100d);

        for (var page = 0; page < maxPages; page++)
        {
            var parameters = new List<string>
            {
                $"query={Uri.EscapeDataString(query)}",
                "max_results=100",
                "tweet.fields=id,text,created_at,author_id,public_metrics,geo",
                "expansions=author_id,geo.place_id",
                "user.fields=id,name,username,location",
                $"place.fields={Uri.EscapeDataString(GeoFields)}",
            };
            if (!string.IsNullOrWhiteSpace(nextToken))
                parameters.Add($"next_token={Uri.EscapeDataString(nextToken)}");

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"tweets/search/recent?{string.Join('&', parameters)}"
            );
            request.Headers.Authorization = new("Bearer", options.Value.OfficialApis.XBearerToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    "La búsqueda oficial de X no tuvo éxito.",
                    null,
                    response.StatusCode
                );
            }

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken
            );
            var root = document.RootElement;
            var users = ReadUsers(root);
            var places = ReadPlaces(root);
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var tweet in data.EnumerateArray())
                {
                    var comment = MapTweet(tweet, users, places);
                    if (comment is not null)
                        comments.Add(comment);
                }
            }

            nextToken =
                root.TryGetProperty("meta", out var meta)
                && meta.TryGetProperty("next_token", out var token)
                    ? token.GetString()
                    : null;
            if (string.IsNullOrWhiteSpace(nextToken) || comments.Count >= maxComments)
                break;
        }

        return comments
            .OrderByDescending(comment => comment.PublishedAt)
            .Take(maxComments)
            .ToArray();
    }

    private static Dictionary<string, (string Name, string Username)> ReadUsers(JsonElement root)
    {
        var users = new Dictionary<string, (string Name, string Username)>(StringComparer.Ordinal);
        if (
            !root.TryGetProperty("includes", out var includes)
            || !includes.TryGetProperty("users", out var values)
            || values.ValueKind != JsonValueKind.Array
        )
            return users;

        foreach (var user in values.EnumerateArray())
        {
            var id = GetString(user, "id");
            if (!string.IsNullOrWhiteSpace(id))
                users[id] = (
                    GetString(user, "name") ?? "Usuario X",
                    GetString(user, "username") ?? "usuario"
                );
        }

        return users;
    }

    private static Dictionary<string, string> ReadPlaces(JsonElement root)
    {
        var places = new Dictionary<string, string>(StringComparer.Ordinal);
        if (
            !root.TryGetProperty("includes", out var includes)
            || !includes.TryGetProperty("places", out var values)
            || values.ValueKind != JsonValueKind.Array
        )
            return places;

        foreach (var place in values.EnumerateArray())
        {
            var id = GetString(place, "id");
            var name = GetString(place, "full_name") ?? GetString(place, "name");
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                places[id] = name;
        }

        return places;
    }

    private static SocialCommentDraft? MapTweet(
        JsonElement tweet,
        IReadOnlyDictionary<string, (string Name, string Username)> users,
        IReadOnlyDictionary<string, string> places
    )
    {
        var id = GetString(tweet, "id");
        var text = GetString(tweet, "text");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text))
            return null;

        var authorId = GetString(tweet, "author_id") ?? string.Empty;
        users.TryGetValue(authorId, out var author);
        var username = string.IsNullOrWhiteSpace(author.Username) ? "usuario" : author.Username;
        var url = $"https://x.com/{Uri.EscapeDataString(username)}/status/{id}";
        var placeName =
            tweet.TryGetProperty("geo", out var geo)
            && GetString(geo, "place_id") is { } placeId
            && places.TryGetValue(placeId, out var foundPlace)
                ? foundPlace
                : null;
        var publishedAt = DateTimeOffset.TryParse(
            GetString(tweet, "created_at"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var timestamp
        )
            ? timestamp
            : DateTimeOffset.UtcNow;
        var likes =
            tweet.TryGetProperty("public_metrics", out var metrics)
            && metrics.TryGetProperty("like_count", out var likeCount)
            && likeCount.TryGetInt32(out var parsedLikes)
                ? parsedLikes
                : (int?)null;

        return new SocialCommentDraft(
            id,
            "x",
            string.IsNullOrWhiteSpace(author.Name) ? username : author.Name,
            $"@{username}",
            $"https://x.com/{Uri.EscapeDataString(username)}",
            text,
            url,
            text.Length > 180 ? $"{text[..177]}..." : text,
            url,
            publishedAt,
            Reactions: likes,
            PlaceName: placeName,
            ContentType: "post"
        );
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? BuildQuery(string? topic, string? search, string? sourceUrl)
    {
        var terms = string.IsNullOrWhiteSpace(search) ? topic?.Trim() : search.Trim();
        if (!string.IsNullOrWhiteSpace(search) && !string.IsNullOrWhiteSpace(topic))
            terms = $"({search.Trim()}) {topic.Trim()}";

        if (string.IsNullOrWhiteSpace(terms))
        {
            if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var postUri))
            {
                terms =
                    StatusIdRegex().IsMatch(postUri.AbsolutePath) ? $"url:{postUri.AbsoluteUri}"
                    : ExtractHandle(sourceUrl) is { } handle ? $"from:{handle}"
                    : null;
            }
        }

        if (string.IsNullOrWhiteSpace(terms))
            return null;

        var tokens = BooleanTokenRegex().Matches(terms).Select(match => match.Value).ToArray();
        var translated = new List<string>();
        for (var index = 0; index < tokens.Length; index++)
        {
            if (tokens[index].Equals("AND", StringComparison.OrdinalIgnoreCase))
                continue;
            if (tokens[index].Equals("NOT", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < tokens.Length)
                    translated.Add($"-{tokens[++index]}");
                continue;
            }

            translated.Add(tokens[index]);
        }

        return string.Join(' ', translated);
    }

    private static string? ExtractHandle(string? sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
            return null;
        var match = HandleRegex().Match(uri.AbsolutePath);
        return
            match.Success && !match.Groups[1].Value.Equals("i", StringComparison.OrdinalIgnoreCase)
            ? match.Groups[1].Value
            : null;
    }

    [GeneratedRegex("\"[^\"]+\"|\\b(?:AND|OR|NOT)\\b|[()]|[^\\s()]+", RegexOptions.IgnoreCase)]
    private static partial Regex BooleanTokenRegex();

    [GeneratedRegex("^/([^/]+)/?")]
    private static partial Regex HandleRegex();

    [GeneratedRegex("/status/\\d+(?:/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex StatusIdRegex();
}
