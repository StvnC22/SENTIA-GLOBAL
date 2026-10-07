using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Infrastructure.SocialListening.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.SocialListening.Apify;

public sealed class ApifySocialCommentProvider(
    ApifyClient apifyClient,
    IApiCredentialStore credentialStore,
    IOptions<SocialListeningOptions> options,
    IMemoryCache cache,
    ILogger<ApifySocialCommentProvider> logger
) : ISocialCommentProvider
{
    private static readonly string[] Platforms = ["instagram", "facebook", "tiktok", "x"];

    public async Task<SocialCommentFetchResult> FetchAsync(
        string? platform,
        bool refresh,
        CancellationToken cancellationToken,
        string? topic = null,
        IReadOnlyDictionary<string, string>? sourceUrls = null
    )
    {
        var credentials = await credentialStore.GetAsync(cancellationToken);
        var settings = options.Value;
        var normalizedTopic = topic?.Trim() ?? string.Empty;
        var sourceFingerprint = BuildSourceFingerprint(sourceUrls);
        var cacheKey =
            $"social-comments:v5:{platform ?? "all"}:{normalizedTopic.ToLowerInvariant()}:{sourceFingerprint}";
        if (
            !refresh
            && cache.TryGetValue(cacheKey, out SocialCommentFetchResult? cached)
            && cached is not null
        )
        {
            return cached;
        }

        var targetPlatforms = ResolveTargetPlatforms(platform, sourceUrls, settings);
        if (targetPlatforms.Count == 0)
        {
            // Sin URLs: vacío total, sin mensaje ni datos demostrativos.
            return SocialCommentFetchResult.Live([]);
        }

        var comments = new List<SocialCommentDraft>();
        var errors = new List<string>();
        var perPlatformLimit = Math.Max(1, settings.MaxCommentsPerPlatform);

        // Una red a la vez: el plan gratuito de Apify limita corridas concurrentes
        // y un timeout global hacía que TikTok dejara a las demás en 0.
        foreach (var targetPlatform in targetPlatforms)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var profileUrl = ResolveProfileUrl(targetPlatform, sourceUrls, settings);
            if (string.IsNullOrWhiteSpace(profileUrl))
            {
                // Campo vacío: se omite en silencio (no mensaje, no consulta).
                continue;
            }

            if (!TryValidateProfileUrl(targetPlatform, profileUrl, out var urlError))
            {
                errors.Add(urlError);
                continue;
            }

            var apifyToken = ResolveApifyToken(credentials, targetPlatform);
            if (string.IsNullOrWhiteSpace(apifyToken))
            {
                errors.Add(
                    "Configura el token de Apify en cualquiera de los campos de red social para obtener comentarios reales."
                );
                continue;
            }

            using var platformTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            platformTimeout.CancelAfter(TimeSpan.FromMinutes(3));

            try
            {
                var platformComments = targetPlatform switch
                {
                    "tiktok" => await FetchTikTokAsync(
                        profileUrl,
                        apifyToken,
                        settings,
                        platformTimeout.Token
                    ),
                    "instagram" => await FetchInstagramAsync(
                        profileUrl,
                        apifyToken,
                        settings,
                        platformTimeout.Token
                    ),
                    "facebook" => await FetchFacebookAsync(
                        profileUrl,
                        apifyToken,
                        settings,
                        platformTimeout.Token
                    ),
                    "x" => await FetchXAsync(
                        profileUrl,
                        apifyToken,
                        settings,
                        platformTimeout.Token,
                        normalizedTopic
                    ),
                    _ => [],
                };

                var kept = platformComments
                    .OrderByDescending(comment => comment.PublishedAt)
                    .Take(perPlatformLimit)
                    .ToArray();
                comments.AddRange(kept);

                logger.LogInformation(
                    "Red {Platform}: {Count} comentarios obtenidos (tope {Limit})",
                    targetPlatform,
                    kept.Length,
                    perPlatformLimit
                );

                if (kept.Length == 0)
                {
                    errors.Add(
                        $"Apify no devolvió comentarios de {targetPlatform}. Revisa créditos, token y que el perfil sea público."
                    );
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                errors.Add(
                    $"La consulta de {targetPlatform} superó el tiempo límite. Intenta de nuevo solo esa red."
                );
                logger.LogWarning(
                    "Timeout al obtener comentarios de {Platform}",
                    targetPlatform
                );
            }
            catch (Exception exception)
            {
                errors.Add(
                    exception is HttpRequestException
                        ? $"No se pudo conectar con Apify para {targetPlatform}. Verifica Internet, firewall y el token."
                        : exception.Message
                );
                logger.LogWarning(
                    exception,
                    "No se pudieron obtener comentarios de {Platform}",
                    targetPlatform
                );
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedTopic))
        {
            comments = comments.Where(comment => MatchesTopic(comment, normalizedTopic)).ToList();
        }

        // Algunos actores repiten el mismo comentario; una fila por red+id.
        comments = comments
            .GroupBy(
                comment => $"{comment.Platform}:{comment.Id}",
                StringComparer.OrdinalIgnoreCase
            )
            .Select(group => group.First())
            .OrderByDescending(comment => comment.PublishedAt)
            .ToList();

        // Tope global sin borrar redes: recorta de forma equitativa por plataforma.
        var totalLimit = Math.Max(1, settings.MaxTotalComments);
        if (comments.Count > totalLimit)
        {
            comments = comments
                .GroupBy(comment => comment.Platform, StringComparer.OrdinalIgnoreCase)
                .SelectMany(group =>
                    group
                        .OrderByDescending(comment => comment.PublishedAt)
                        .Take(Math.Max(1, totalLimit / Math.Max(1, targetPlatforms.Count)))
                )
                .OrderByDescending(comment => comment.PublishedAt)
                .ToList();
        }

        var lastError = errors.Count == 0 ? null : string.Join(" · ", errors.Distinct());

        if (comments.Count == 0)
        {
            return new SocialCommentFetchResult(
                "live",
                [],
                lastError
                    ?? "Apify no devolvió comentarios. Verifica tu token, créditos y que los perfiles sean públicos."
            );
        }

        var result =
            errors.Count == 0
                ? SocialCommentFetchResult.Live(comments)
                : new SocialCommentFetchResult("live", comments, lastError);
        cache.Set(cacheKey, result, TimeSpan.FromMinutes(settings.CacheMinutes));
        return result;
    }

    private static List<string> ResolveTargetPlatforms(
        string? platform,
        IReadOnlyDictionary<string, string>? sourceUrls,
        SocialListeningOptions settings
    )
    {
        if (!string.IsNullOrWhiteSpace(platform))
        {
            var name = platform.Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(ResolveProfileUrl(name, sourceUrls, settings))
                ? []
                : [name];
        }

        var requested = Platforms
            .Where(name => !string.IsNullOrWhiteSpace(ResolveProfileUrl(name, sourceUrls, settings)))
            .ToList();

        // Si el panel no envió URLs, caer a los perfiles configurados.
        return requested.Count > 0
            ? requested
            : Platforms
                .Where(name => !string.IsNullOrWhiteSpace(settings.Profiles.GetValueOrDefault(name)))
                .ToList();
    }

    private static string? ResolveProfileUrl(
        string platform,
        IReadOnlyDictionary<string, string>? sourceUrls,
        SocialListeningOptions settings
    )
    {
        if (
            sourceUrls?.TryGetValue(platform, out var requestedUrl) == true
            && !string.IsNullOrWhiteSpace(requestedUrl)
        )
        {
            return requestedUrl.Trim();
        }

        return settings.Profiles.GetValueOrDefault(platform);
    }

    private static bool TryValidateProfileUrl(string platform, string profileUrl, out string error)
    {
        error = string.Empty;
        var trimmed = profileUrl.Trim();
        var platformName = platform switch
        {
            "instagram" => "Instagram",
            "facebook" => "Facebook",
            "tiktok" => "TikTok",
            "x" => "X",
            _ => platform,
        };

        if (
            !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        )
        {
            error =
                $"URL de {platformName} incorrecta: usa un enlace completo (https://…).";
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath.Trim('/');
        var ok = platform switch
        {
            "instagram" => (host.Contains("instagram.com") || host == "instagr.am")
                && path.Length > 0
                && !path.Equals("p", StringComparison.OrdinalIgnoreCase),
            "facebook" => IsValidFacebookUrl(host, path, uri),
            "tiktok" => host.Contains("tiktok.com") && path.Length > 0,
            "x" => (host.Contains("x.com") || host.Contains("twitter.com")) && path.Length > 0,
            _ => true,
        };

        if (!ok)
        {
            error = platform switch
            {
                "instagram" =>
                    "URL de Instagram incorrecta. Ejemplo: https://www.instagram.com/cuenta/",
                "facebook" =>
                    "URL de Facebook incorrecta. Ejemplo: https://www.facebook.com/pagina",
                "tiktok" =>
                    "URL de TikTok incorrecta. Ejemplo: https://www.tiktok.com/@cuenta o .../video/123",
                "x" =>
                    "URL de X incorrecta. Ejemplo: https://x.com/cuenta o .../status/123",
                _ => $"URL de {platformName} incorrecta.",
            };
            return false;
        }

        return true;
    }

    private static bool IsValidFacebookUrl(string host, string path, Uri uri)
    {
        if (!(host.Contains("facebook.com") || host.Contains("fb.com") || host.Contains("fb.watch")))
        {
            return false;
        }

        // profile.php?id=123 es una URL válida de Facebook.
        if (path.Equals("profile.php", StringComparison.OrdinalIgnoreCase))
        {
            return uri.Query.Contains("id=", StringComparison.OrdinalIgnoreCase);
        }

        return path.Length > 0 || uri.Query.Length > 1;
    }

    private static string BuildSourceFingerprint(IReadOnlyDictionary<string, string>? sourceUrls)
    {
        if (sourceUrls is null || sourceUrls.Count == 0)
        {
            return "default";
        }

        return string.Join(
            '|',
            sourceUrls
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key.ToLowerInvariant()}:{pair.Value.Trim().ToLowerInvariant()}")
        );
    }

    private static string? ResolveApifyToken(ApiCredentials credentials, string platform) =>
        // Apify usa el mismo token para sus actores de las distintas redes. Se
        // aceptan todos los campos para evitar que una clave guardada en una
        // red quede inutilizada por la selección de otra pestaña.
        platform switch
        {
            "tiktok" => FirstNonEmptyCredential(
                credentials.TikTok,
                credentials.X,
                credentials.Instagram,
                credentials.Facebook
            ),
            _ => FirstNonEmptyCredential(
                credentials.X,
                credentials.TikTok,
                credentials.Instagram,
                credentials.Facebook
            ),
        };

    private static string? FirstNonEmptyCredential(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private async Task<IReadOnlyList<SocialCommentDraft>> FetchTikTokAsync(
        string profileUrl,
        string apifyToken,
        SocialListeningOptions settings,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(settings.Actors.TikTokComments))
        {
            throw new InvalidOperationException(
                "Falta el actor de comentarios de TikTok en la configuración."
            );
        }

        // URL de un video concreto → comentarios de esa publicación.
        if (TryParseTikTokVideoUrl(profileUrl, out var videoUrl, out _))
        {
            var commentsLimit = Math.Max(
                settings.MaxCommentsPerPost,
                Math.Min(settings.MaxCommentsPerPlatform, 200)
            );
            logger.LogInformation("TikTok: comentarios del video {VideoUrl}", videoUrl);
            var fromVideo = await apifyClient.RunActorSyncAsync(
                settings.Actors.TikTokComments,
                new
                {
                    postURLs = new[] { videoUrl },
                    commentsPerPost = commentsLimit,
                    maxRepliesPerComment = 0,
                },
                apifyToken,
                cancellationToken
            );

            return fromVideo
                .Select(item => MapTikTokComment(item, videoUrl))
                .Where(comment => comment is not null)
                .Cast<SocialCommentDraft>()
                .ToArray();
        }

        var profileName = ExtractProfileName("tiktok", profileUrl);

        // 1) Abrir videos del perfil y sacar comentarios.
        var fromProfile = await apifyClient.RunActorSyncAsync(
            settings.Actors.TikTokComments,
            new
            {
                profiles = new[] { profileName },
                resultsPerPage = settings.MaxPostsPerProfile,
                commentsPerPost = settings.MaxCommentsPerPost,
                profileSorting = "latest",
                excludePinnedPosts = false,
            },
            apifyToken,
            cancellationToken
        );

        var mapped = fromProfile
            .Select(item => MapTikTokComment(item, profileUrl))
            .Where(comment => comment is not null)
            .Cast<SocialCommentDraft>()
            .ToArray();

        if (mapped.Length > 0)
        {
            return mapped;
        }

        if (string.IsNullOrWhiteSpace(settings.Actors.TikTokPosts))
        {
            return [];
        }

        // 2) Respaldo: listar videos y pedir comentarios por URL.
        var videos = await apifyClient.RunActorSyncAsync(
            settings.Actors.TikTokPosts,
            new
            {
                profiles = new[] { profileName },
                resultsPerPage = settings.MaxPostsPerProfile,
                profileSorting = "latest",
                excludePinnedPosts = false,
            },
            apifyToken,
            cancellationToken
        );

        var videoUrls = videos
            .Select(ExtractVideoUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(settings.MaxPostsPerProfile)
            .ToArray();

        if (videoUrls.Length == 0)
        {
            logger.LogWarning("TikTok no devolvió videos públicos para {Profile}", profileName);
            return [];
        }

        var fromVideos = await apifyClient.RunActorSyncAsync(
            settings.Actors.TikTokComments,
            new
            {
                postURLs = videoUrls,
                commentsPerPost = settings.MaxCommentsPerPost,
                maxRepliesPerComment = 0,
            },
            apifyToken,
            cancellationToken
        );

        return fromVideos
            .Select(item => MapTikTokComment(item, profileUrl))
            .Where(comment => comment is not null)
            .Cast<SocialCommentDraft>()
            .ToArray();
    }

    private static string ExtractVideoUrl(JsonElement item)
    {
        var values = ExtractString(item);
        if (!string.IsNullOrWhiteSpace(values("errorCode")))
        {
            return string.Empty;
        }

        var url = FirstNonEmpty(
            values("webVideoUrl"),
            values("videoUrl"),
            values("url"),
            values("submittedVideoUrl")
        );

        return url.Contains("/video/", StringComparison.OrdinalIgnoreCase) ? url : string.Empty;
    }

    private static SocialCommentDraft? MapTikTokComment(JsonElement item, string profileUrl)
    {
        var values = ExtractString(item);
        if (!string.IsNullOrWhiteSpace(values("errorCode")))
        {
            return null;
        }

        var text = FirstNonEmpty(values("text"), values("comment"), values("commentText"));
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var username = FirstNonEmpty(
            values("uniqueId"),
            values("unique_id"),
            values("nickname"),
            values("author"),
            "usuario"
        );
        var videoUrl = FirstNonEmpty(values("videoWebUrl"), values("webVideoUrl"), profileUrl);

        return new SocialCommentDraft(
            FirstNonEmpty(values("cid"), values("id"), Guid.NewGuid().ToString("N")),
            "tiktok",
            FirstNonEmpty(values("nickname"), values("uniqueId"), username),
            username.StartsWith('@') ? username : $"@{username}",
            FirstNonEmpty(
                values("profileUrl"),
                $"https://www.tiktok.com/@{username.TrimStart('@')}"
            ),
            text,
            FirstNonEmpty(values("commentUrl"), videoUrl),
            TruncatePostTitle(
                FirstNonEmpty(values("videoText"), values("desc"), "Video de TikTok")
            ),
            videoUrl,
            ParseTimestamp(
                values("createTimeISO"),
                values("createTime"),
                values("timestamp"),
                values("createdAt")
            )
        );
    }

    private async Task<IReadOnlyList<SocialCommentDraft>> FetchInstagramAsync(
        string profileUrl,
        string apifyToken,
        SocialListeningOptions settings,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(settings.Actors.InstagramPosts))
        {
            return await FetchProfileCommentsAsync(
                "instagram",
                profileUrl,
                apifyToken,
                settings,
                cancellationToken
            );
        }

        var instagramPostLimit = Math.Max(settings.MaxPostsPerProfile, settings.InstagramMaxPosts);
        var instagramCommentsPerPost = Math.Max(
            settings.MaxCommentsPerPost,
            settings.InstagramMaxCommentsPerPost
        );
        var postsRaw = await apifyClient.RunActorSyncAsync(
            settings.Actors.InstagramPosts,
            new
            {
                directUrls = new[] { profileUrl },
                resultsType = "posts",
                resultsLimit = instagramPostLimit,
            },
            apifyToken,
            cancellationToken
        );

        var posts = ExtractSocialPosts(postsRaw, "instagram", instagramPostLimit);
        // Quedarse con publicaciones del periodo largo (~1 año) cuando el actor
        // devuelve más de las necesarias.
        var cutoff = DateTimeOffset.UtcNow.AddDays(-400);
        posts = posts.Where(post => post.PublishedAt >= cutoff).Take(instagramPostLimit).ToArray();
        if (posts.Count == 0)
        {
            logger.LogWarning("Instagram no devolvió publicaciones para {Profile}", profileUrl);
            return await FetchProfileCommentsAsync(
                "instagram",
                profileUrl,
                apifyToken,
                settings,
                cancellationToken
            );
        }

        logger.LogInformation(
            "Instagram: revisando comentarios en {PostCount} publicaciones (tope {Limit})",
            posts.Count,
            instagramPostLimit
        );

        var comments = await FetchCommentsForPostsAsync(
            settings.Actors.InstagramComments,
            posts,
            apifyToken,
            settings,
            cancellationToken,
            MapInstagramComment,
            instagramCommentsPerPost,
            batchSizeOverride: 6
        );

        if (comments.Count > 0)
        {
            return comments;
        }

        return await FetchProfileCommentsAsync(
            "instagram",
            profileUrl,
            apifyToken,
            settings,
            cancellationToken
        );
    }

    private async Task<IReadOnlyList<SocialCommentDraft>> FetchFacebookAsync(
        string profileUrl,
        string apifyToken,
        SocialListeningOptions settings,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(settings.Actors.FacebookPosts))
        {
            return await FetchProfileCommentsAsync(
                "facebook",
                profileUrl,
                apifyToken,
                settings,
                cancellationToken
            );
        }

        var facebookPostLimit = Math.Max(settings.MaxPostsPerProfile, settings.FacebookMaxPosts);
        var facebookCommentsPerPost = Math.Max(
            settings.MaxCommentsPerPost,
            settings.FacebookMaxCommentsPerPost
        );
        var newerThan = DateTime.UtcNow.AddDays(-400).ToString("yyyy-MM-dd");
        var postsRaw = await apifyClient.RunActorSyncAsync(
            settings.Actors.FacebookPosts,
            new
            {
                startUrls = new[] { new { url = profileUrl } },
                maxPosts = facebookPostLimit,
                resultsLimit = facebookPostLimit,
                // Prioriza publicaciones del último año aproximadamente.
                onlyPostsNewerThan = newerThan,
            },
            apifyToken,
            cancellationToken
        );

        var posts = ExtractSocialPosts(postsRaw, "facebook", facebookPostLimit);
        if (posts.Count == 0)
        {
            logger.LogWarning("Facebook no devolvió publicaciones para {Profile}", profileUrl);
            return await FetchProfileCommentsAsync(
                "facebook",
                profileUrl,
                apifyToken,
                settings,
                cancellationToken
            );
        }

        logger.LogInformation(
            "Facebook: revisando comentarios en {PostCount} publicaciones (tope {Limit})",
            posts.Count,
            facebookPostLimit
        );

        var comments = await FetchCommentsForPostsAsync(
            settings.Actors.FacebookComments,
            posts,
            apifyToken,
            settings,
            cancellationToken,
            MapFacebookComment,
            facebookCommentsPerPost,
            batchSizeOverride: 6
        );

        if (comments.Count > 0)
        {
            return comments;
        }

        return await FetchProfileCommentsAsync(
            "facebook",
            profileUrl,
            apifyToken,
            settings,
            cancellationToken
        );
    }

    private async Task<IReadOnlyList<SocialCommentDraft>> FetchCommentsForPostsAsync(
        string commentsActor,
        IReadOnlyList<SocialPost> posts,
        string apifyToken,
        SocialListeningOptions settings,
        CancellationToken cancellationToken,
        Func<JsonElement, SocialPost, SocialCommentDraft?> mapComment,
        int? commentsPerPostOverride = null,
        int? batchSizeOverride = null
    )
    {
        if (string.IsNullOrWhiteSpace(commentsActor) || posts.Count == 0)
        {
            return [];
        }

        var commentsPerPost = Math.Max(
            1,
            commentsPerPostOverride ?? settings.MaxCommentsPerPost
        );
        var batchSize = Math.Max(1, batchSizeOverride ?? settings.CommentUrlBatchSize);
        var batches = posts
            .Select((post, index) => (post, index))
            .GroupBy(item => item.index / batchSize, item => item.post)
            .Select(group => group.ToArray())
            .ToArray();

        // Plan gratuito de Apify: pocas corridas concurrentes.
        using var gate = new SemaphoreSlim(2, 2);
        var tasks = batches.Select(async batch =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var postUrls = batch.Select(post => post.Url).ToArray();
                var items = await apifyClient.RunActorSyncAsync(
                    commentsActor,
                    new
                    {
                        directUrls = postUrls,
                        startUrls = postUrls.Select(url => new { url }).ToArray(),
                        resultsLimit = commentsPerPost,
                        maxComments = commentsPerPost,
                    },
                    apifyToken,
                    cancellationToken
                );

                var postByUrl = batch.ToDictionary(
                    post => post.Url,
                    post => post,
                    StringComparer.OrdinalIgnoreCase
                );

                return items
                    .Select(item =>
                    {
                        var post = ResolvePostForComment(item, postByUrl, batch);
                        return post is null ? null : mapComment(item, post);
                    })
                    .Where(comment => comment is not null)
                    .Cast<SocialCommentDraft>()
                    .ToArray();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "No se pudieron obtener comentarios de un lote");
                return Array.Empty<SocialCommentDraft>();
            }
            finally
            {
                gate.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results
            .SelectMany(batch => batch)
            .GroupBy(comment => comment.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(comment => comment.PublishedAt)
            .ToArray();
    }

    private static SocialPost? ResolvePostForComment(
        JsonElement item,
        IReadOnlyDictionary<string, SocialPost> postByUrl,
        IReadOnlyList<SocialPost> batch
    )
    {
        var values = ExtractString(item);
        var postUrl = FirstNonEmpty(
            values("postUrl"),
            values("inputUrl"),
            values("facebookUrl"),
            values("facebookurl"),
            values("url")
        );

        if (
            !string.IsNullOrWhiteSpace(postUrl)
            && postByUrl.TryGetValue(postUrl, out var matchedPost)
        )
        {
            return matchedPost;
        }

        return batch.FirstOrDefault();
    }

    private static IReadOnlyList<SocialPost> ExtractSocialPosts(
        IReadOnlyList<JsonElement> items,
        string platform,
        int maxCount
    ) =>
        items
            .Select(item =>
            {
                var values = ExtractString(item);
                var url = FirstNonEmpty(
                    values("url"),
                    values("postUrl"),
                    values("topLevelUrl"),
                    values("facebookUrl"),
                    values("inputUrl"),
                    BuildInstagramPostUrl(values("shortCode"))
                );

                if (string.IsNullOrWhiteSpace(url))
                {
                    return null;
                }

                return new SocialPost(
                    FirstNonEmpty(
                        values("id"),
                        values("postId"),
                        values("shortCode"),
                        url
                    ),
                    platform,
                    FirstNonEmpty(
                        values("caption"),
                        values("text"),
                        values("message"),
                        values("postText"),
                        "Publicación"
                    ),
                    url,
                    ParseTimestamp(
                        values("timestamp"),
                        values("time"),
                        values("createdAt"),
                        values("takenAt"),
                        values("postedAt")
                    )
                );
            })
            .Where(post => post is not null)
            .Cast<SocialPost>()
            .DistinctBy(post => post.Url, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(post => post.PublishedAt)
            .Take(maxCount)
            .ToArray();

    private static string BuildInstagramPostUrl(string shortCode) =>
        string.IsNullOrWhiteSpace(shortCode)
            ? string.Empty
            : $"https://www.instagram.com/p/{shortCode}/";

    private static SocialCommentDraft? MapInstagramComment(JsonElement item, SocialPost post)
    {
        var values = ExtractString(item);
        var text = FirstNonEmpty(values("text"), values("comment"), values("commentText"));
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var username = FirstNonEmpty(
            values("ownerUsername"),
            values("username"),
            values("commentAuthor"),
            values("author"),
            "usuario"
        );

        return new SocialCommentDraft(
            FirstNonEmpty(values("id"), values("commentId"), Guid.NewGuid().ToString("N")),
            "instagram",
            FirstNonEmpty(values("ownerFullName"), values("name"), username),
            username.StartsWith('@') ? username : $"@{username}",
            FirstNonEmpty(
                values("ownerProfilePicUrl"),
                values("profileUrl"),
                $"https://www.instagram.com/{username.TrimStart('@')}/"
            ),
            text,
            FirstNonEmpty(values("commentUrl"), post.Url),
            TruncatePostTitle(post.Title),
            post.Url,
            ParseTimestamp(
                values("timestamp"),
                values("commentTimestamp"),
                values("createdAt"),
                values("createTimeISO")
            )
        );
    }

    private static SocialCommentDraft? MapFacebookComment(JsonElement item, SocialPost post)
    {
        var values = ExtractString(item);
        var text = FirstNonEmpty(values("text"), values("comment"), values("commentText"));
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var author = FirstNonEmpty(
            values("profileName"),
            values("author"),
            values("commentAuthor"),
            values("name"),
            "Usuario"
        );
        var profileUrl = FirstNonEmpty(values("profileUrl"), values("profilePicture"), post.Url);

        return new SocialCommentDraft(
            FirstNonEmpty(values("id"), values("commentId"), Guid.NewGuid().ToString("N")),
            "facebook",
            author,
            author.StartsWith('@') ? author : $"@{author.Replace(' ', '_').ToLowerInvariant()}",
            profileUrl,
            text,
            FirstNonEmpty(values("commentUrl"), values("facebookUrl"), post.Url),
            TruncatePostTitle(post.Title),
            post.Url,
            ParseTimestamp(
                values("date"),
                values("timestamp"),
                values("commentTimestamp"),
                values("createdAt"),
                values("time")
            )
        );
    }

    private sealed record SocialPost(
        string Id,
        string Platform,
        string Title,
        string Url,
        DateTimeOffset PublishedAt
    );

    private async Task<IReadOnlyList<SocialCommentDraft>> FetchProfileCommentsAsync(
        string platform,
        string profileUrl,
        string apifyToken,
        SocialListeningOptions settings,
        CancellationToken cancellationToken
    )
    {
        var profileName = ExtractProfileName(platform, profileUrl);
        var input = new Dictionary<string, object?>
        {
            ["latestPosts"] = settings.MaxPostsPerProfile,
            ["latestComments"] = settings.MaxCommentsPerPost,
            ["sentimentAnalysis"] = false,
            ["scrapeInstagram"] = platform == "instagram",
            ["scrapeFacebook"] = platform == "facebook",
            ["scrapeTiktok"] = platform == "tiktok",
        };

        switch (platform)
        {
            case "instagram":
                input["instagramProfileName"] = profileName;
                break;
            case "facebook":
                input["facebookProfileName"] = profileName;
                break;
            case "tiktok":
                input["tiktokProfileName"] = profileName;
                break;
        }

        var items = await apifyClient.RunActorSyncAsync(
            settings.Actors.ProfileComments,
            input,
            apifyToken,
            cancellationToken
        );

        return items
            .Select(item => MapProfileComment(item, platform))
            .Where(comment => comment is not null)
            .Cast<SocialCommentDraft>()
            .ToArray();
    }

    private async Task<IReadOnlyList<SocialCommentDraft>> FetchXAsync(
        string profileUrl,
        string apifyToken,
        SocialListeningOptions settings,
        CancellationToken cancellationToken,
        string? topic = null
    )
    {
        // URL de un post concreto → comentarios de ese tweet.
        if (TryParseXStatusUrl(profileUrl, out var tweetId, out var statusHandle))
        {
            var handle = statusHandle ?? "usuario";
            var postUrl = string.IsNullOrWhiteSpace(statusHandle)
                ? $"https://x.com/i/status/{tweetId}"
                : $"https://x.com/{statusHandle}/status/{tweetId}";
            var post = new XTweetPost(
                tweetId,
                "Publicación de X",
                postUrl,
                DateTimeOffset.UtcNow
            );
            var pages = Math.Max(settings.XMaxCommentPages, 10);
            logger.LogInformation("X: comentarios del post {TweetId}", tweetId);

            try
            {
                var tweetComments = await apifyClient.RunActorSyncAsync(
                    settings.Actors.XComments,
                    new { tweetId, maxPages = pages },
                    apifyToken,
                    cancellationToken
                );

                return tweetComments
                    .Select(item => MapXComment(item, post, handle))
                    .Where(comment => comment is not null)
                    .Cast<SocialCommentDraft>()
                    .ToArray();
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "No se pudieron obtener comentarios del tweet {TweetId}",
                    tweetId
                );
                throw;
            }
        }

        var handleFromProfile = ExtractProfileName("x", profileUrl);

        // Una red a la vez: evita saturar el límite gratuito de Apify.
        var postComments = await FetchXPostCommentsAsync(
            profileUrl,
            handleFromProfile,
            apifyToken,
            settings,
            cancellationToken,
            topic
        );
        var replyComments = await FetchXRepliesAsync(
            handleFromProfile,
            profileUrl,
            apifyToken,
            settings,
            cancellationToken,
            topic
        );

        return postComments
            .Concat(replyComments)
            .GroupBy(comment => comment.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(comment => comment.PublishedAt)
            .ToArray();
    }

    private async Task<IReadOnlyList<SocialCommentDraft>> FetchXRepliesAsync(
        string handle,
        string profileUrl,
        string apifyToken,
        SocialListeningOptions settings,
        CancellationToken cancellationToken,
        string? topic = null
    )
    {
        var maxReplies = Math.Max(settings.MaxCommentsPerPost * 3, 100);
        var items = await apifyClient.RunActorSyncAsync(
            settings.Actors.XPosts,
            new
            {
                searchTerms = string.IsNullOrWhiteSpace(topic)
                    ? new[] { $"to:{handle}", $"@{handle}" }
                    : new[] { $"{topic} to:{handle}", $"{topic} @{handle}" },
                maxItems = maxReplies,
                sort = "Latest",
            },
            apifyToken,
            cancellationToken
        );

        return items
            .Select(item => MapXReply(item, profileUrl, handle))
            .Where(comment => comment is not null)
            .Cast<SocialCommentDraft>()
            .ToArray();
    }

    private async Task<IReadOnlyList<SocialCommentDraft>> FetchXPostCommentsAsync(
        string profileUrl,
        string handle,
        string apifyToken,
        SocialListeningOptions settings,
        CancellationToken cancellationToken,
        string? topic = null
    )
    {
        var maxPosts = Math.Max(settings.XMaxPosts, settings.MaxPostsPerProfile);

        IReadOnlyList<JsonElement> postsRaw = [];
        if (string.IsNullOrWhiteSpace(topic))
        {
            postsRaw = await apifyClient.RunActorSyncAsync(
                settings.Actors.XTimeline,
                new { username = handle, maxItems = maxPosts },
                apifyToken,
                cancellationToken
            );
        }

        if (postsRaw.Count == 0 || postsRaw.All(item => !IsUsableTimelineItem(item)))
        {
            postsRaw = await apifyClient.RunActorSyncAsync(
                settings.Actors.XPosts,
                new
                {
                    searchTerms = string.IsNullOrWhiteSpace(topic)
                        ? new[] { $"from:{handle}" }
                        : new[] { $"{topic} from:{handle}", topic },
                    maxItems = maxPosts,
                    sort = "Latest",
                },
                apifyToken,
                cancellationToken
            );
        }

        var posts = ExtractPosts(postsRaw, maxPosts);

        if (posts.Count == 0)
        {
            logger.LogWarning(
                "Apify no devolvió publicaciones recientes de X para {Handle}",
                handle
            );
            return [];
        }

        logger.LogInformation(
            "X: revisando comentarios en {PostCount} publicaciones de @{Handle}",
            posts.Count,
            handle
        );

        using var gate = new SemaphoreSlim(2, 2);
        var tasks = posts.Select(async (post, index) =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var pages =
                    index < settings.XRecentPostsBoost
                        ? Math.Max(settings.XMaxCommentPages, 10)
                        : settings.XMaxCommentPages;

                var tweetComments = await apifyClient.RunActorSyncAsync(
                    settings.Actors.XComments,
                    new { tweetId = post.Id, maxPages = pages },
                    apifyToken,
                    cancellationToken
                );

                return tweetComments
                    .Select(item => MapXComment(item, post, handle))
                    .Where(comment => comment is not null)
                    .Cast<SocialCommentDraft>()
                    .ToArray();
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "No se pudieron obtener comentarios del tweet {TweetId}",
                    post.Id
                );
                return [];
            }
            finally
            {
                gate.Release();
            }
        });

        var batches = await Task.WhenAll(tasks);
        return batches.SelectMany(batch => batch).ToArray();
    }

    private static bool IsUsableTimelineItem(JsonElement item)
    {
        var values = ExtractString(item);
        var id = FirstNonEmpty(
            values("tweetId"),
            values("id"),
            values("id_str"),
            ExtractTweetId(values("permalink")),
            ExtractTweetId(values("url")),
            ExtractTweetId(values("twitterUrl"))
        );
        return IsValidTweetId(id);
    }

    private static IReadOnlyList<XTweetPost> ExtractPosts(
        IReadOnlyList<JsonElement> items,
        int maxCount
    ) =>
        items
            .Select(item =>
            {
                var values = ExtractString(item);
                var id = FirstNonEmpty(
                    values("tweetId"),
                    values("id"),
                    values("id_str"),
                    ExtractTweetId(values("permalink")),
                    ExtractTweetId(values("url")),
                    ExtractTweetId(values("twitterUrl"))
                );

                if (!IsValidTweetId(id))
                {
                    return null;
                }

                var publishedAt = ParseTimestamp(
                    values("createdAt"),
                    values("created_at"),
                    values("timestamp"),
                    values("date")
                );

                return new XTweetPost(
                    id,
                    FirstNonEmpty(values("text"), values("fullText"), "Publicación de X"),
                    FirstNonEmpty(
                        values("permalink"),
                        values("url"),
                        values("twitterUrl"),
                        $"https://x.com/i/status/{id}"
                    ),
                    publishedAt
                );
            })
            .Where(post => post is not null)
            .Cast<XTweetPost>()
            .DistinctBy(post => post.Id, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(post => post.PublishedAt)
            .ThenByDescending(post => post.Id, StringComparer.Ordinal)
            .Take(maxCount)
            .ToArray();

    private sealed record XTweetPost(
        string Id,
        string Text,
        string Url,
        DateTimeOffset PublishedAt
    );

    private static bool IsValidTweetId(string id) => id.Length >= 15 && id.All(char.IsDigit);

    private static SocialCommentDraft? MapProfileComment(JsonElement item, string platform)
    {
        var values = ExtractString(item);
        var text = FirstNonEmpty(values("commentText"), values("text"), values("comment"));
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var author = FirstNonEmpty(values("commentAuthor"), values("author"), "Usuario");
        var postUrl = FirstNonEmpty(values("postUrl"), values("url"));
        var postTitle = FirstNonEmpty(
            values("postDescription"),
            values("postTitle"),
            "Publicación"
        );

        return new SocialCommentDraft(
            FirstNonEmpty(values("commentId"), values("id"), Guid.NewGuid().ToString("N")),
            platform,
            author,
            author.StartsWith('@') ? author : $"@{author}",
            FirstNonEmpty(values("profileUrl"), postUrl),
            text,
            FirstNonEmpty(values("commentUrl"), postUrl),
            postTitle,
            postUrl,
            ParseTimestamp(values("commentTimestamp"), values("timestamp"))
        );
    }

    private static SocialCommentDraft? MapXReply(
        JsonElement item,
        string profileUrl,
        string institutionHandle
    )
    {
        var values = ExtractString(item);
        var text = FirstNonEmpty(values("text"), values("fullText"), values("comment"));
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var (username, displayName) = ExtractAuthor(item);
        username = FirstNonEmpty(
            username,
            ExtractUsernameFromUrl(FirstNonEmpty(values("url"), values("twitterUrl")))
        );

        var mention = ExtractMentionHandle(text);
        if (
            string.IsNullOrWhiteSpace(username)
            && !string.IsNullOrWhiteSpace(mention)
            && !mention.Equals(institutionHandle, StringComparison.OrdinalIgnoreCase)
        )
        {
            username = mention;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            username = "usuario";
        }

        displayName = FirstNonEmpty(
            displayName,
            username.Equals("usuario", StringComparison.OrdinalIgnoreCase) ? null : username,
            "Usuario"
        );

        if (
            string.Equals(username, institutionHandle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                username.TrimStart('@'),
                institutionHandle,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return null;
        }

        var tweetId = FirstNonEmpty(
            values("id"),
            values("tweetId"),
            ExtractTweetId(values("url")),
            ExtractTweetId(values("twitterUrl"))
        );
        var postUrl = IsValidTweetId(tweetId) ? $"https://x.com/i/status/{tweetId}" : profileUrl;
        var commentUrl = FirstNonEmpty(values("url"), values("twitterUrl"), postUrl);

        return new SocialCommentDraft(
            FirstNonEmpty(values("id"), values("tweetId"), Guid.NewGuid().ToString("N")),
            "x",
            displayName,
            username.StartsWith('@') ? username : $"@{username}",
            FirstNonEmpty(values("profileUrl"), $"https://x.com/{username.TrimStart('@')}"),
            text,
            commentUrl,
            FirstNonEmpty(values("inReplyToText"), "Respuesta a ULEAM"),
            postUrl,
            ParseTimestamp(
                values("created_at"),
                values("createdAt"),
                values("timestamp"),
                values("createTimeISO"),
                values("date")
            )
        );
    }

    private static (string Username, string DisplayName) ExtractAuthor(JsonElement item)
    {
        foreach (var propertyName in new[] { "author", "user", "fromUser", "userData" })
        {
            if (
                !item.TryGetProperty(propertyName, out var author)
                || author.ValueKind != JsonValueKind.Object
            )
            {
                continue;
            }

            var authorValues = ExtractString(author);
            var username = FirstNonEmpty(
                authorValues("userName"),
                authorValues("username"),
                authorValues("screen_name"),
                authorValues("screenName"),
                authorValues("uniqueId")
            );
            if (string.IsNullOrWhiteSpace(username))
            {
                continue;
            }

            var displayName = FirstNonEmpty(authorValues("name"), authorValues("nickname"), username);
            return (username, displayName);
        }

        var values = ExtractString(item);
        var fallbackUsername = FirstNonEmpty(
            values("username"),
            values("screenName"),
            values("userName"),
            values("screen_name")
        );
        return (
            fallbackUsername,
            FirstNonEmpty(values("name"), values("nickname"), fallbackUsername)
        );
    }

    private static SocialCommentDraft? MapXComment(
        JsonElement item,
        XTweetPost post,
        string institutionHandle
    )
    {
        var values = ExtractString(item);
        var text = FirstNonEmpty(
            values("comment_text"),
            values("commentText"),
            values("text"),
            values("fullText"),
            values("comment")
        );
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var commentId = FirstNonEmpty(
            values("comment_id"),
            values("commentId"),
            values("id"),
            values("tweetId")
        );
        if (string.Equals(commentId, post.Id, StringComparison.Ordinal))
        {
            return null;
        }

        var (username, displayName) = ExtractAuthor(item);
        username = FirstNonEmpty(
            username,
            values("username"),
            values("screenName"),
            values("userName"),
            ExtractUsernameFromUrl(FirstNonEmpty(values("url"), values("commentUrl"))),
            "usuario"
        );

        if (
            string.Equals(username, institutionHandle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                username.TrimStart('@'),
                institutionHandle,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return null;
        }

        return new SocialCommentDraft(
            FirstNonEmpty(commentId, Guid.NewGuid().ToString("N")),
            "x",
            FirstNonEmpty(
                displayName,
                values("name"),
                values("display_name"),
                values("displayName"),
                username
            ),
            username.StartsWith('@') ? username : $"@{username}",
            FirstNonEmpty(values("profileUrl"), $"https://x.com/{username.TrimStart('@')}"),
            text,
            FirstNonEmpty(values("url"), values("commentUrl"), post.Url),
            TruncatePostTitle(post.Text),
            post.Url,
            ParseTimestamp(
                values("created_at"),
                values("createdAt"),
                values("timestamp"),
                values("createTimeISO"),
                values("createTime"),
                values("date")
            )
        );
    }

    private static string TruncatePostTitle(string text) =>
        text.Length <= 80 ? text : $"{text[..77]}…";

    private static string ExtractProfileName(string platform, string profileUrl)
    {
        if (TryParseTikTokVideoUrl(profileUrl, out _, out var tiktokUser) && platform == "tiktok")
        {
            return tiktokUser ?? string.Empty;
        }

        if (TryParseXStatusUrl(profileUrl, out _, out var xHandle) && platform == "x")
        {
            return xHandle ?? string.Empty;
        }

        if (!Uri.TryCreate(profileUrl, UriKind.Absolute, out var uri))
        {
            var segments = profileUrl.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var last = segments.LastOrDefault() ?? string.Empty;
            return platform == "tiktok" ? last.TrimStart('@') : last;
        }

        var pathSegments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var name = pathSegments.FirstOrDefault() ?? string.Empty;
        if (platform == "tiktok")
        {
            name = name.TrimStart('@');
        }

        return name;
    }

    private static bool TryParseTikTokVideoUrl(
        string url,
        out string cleanVideoUrl,
        out string? username
    )
    {
        cleanVideoUrl = string.Empty;
        username = null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        var match = Regex.Match(
            uri.AbsolutePath,
            @"/@(?<user>[^/]+)/video/(?<id>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
        if (!match.Success)
        {
            return false;
        }

        username = match.Groups["user"].Value;
        cleanVideoUrl = $"https://www.tiktok.com/@{username}/video/{match.Groups["id"].Value}";
        return true;
    }

    private static bool TryParseXStatusUrl(string url, out string tweetId, out string? handle)
    {
        tweetId = string.Empty;
        handle = null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        var withHandle = Regex.Match(
            uri.AbsolutePath,
            @"/(?<user>[^/]+)/status/(?<id>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
        if (withHandle.Success)
        {
            tweetId = withHandle.Groups["id"].Value;
            var user = withHandle.Groups["user"].Value;
            handle = user.Equals("i", StringComparison.OrdinalIgnoreCase) ? null : user;
            return IsValidTweetId(tweetId);
        }

        var statusOnly = Regex.Match(
            uri.AbsolutePath,
            @"/status/(?<id>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
        if (!statusOnly.Success)
        {
            return false;
        }

        tweetId = statusOnly.Groups["id"].Value;
        return IsValidTweetId(tweetId);
    }

    private static string ExtractTweetId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (TryParseXStatusUrl(url, out var tweetId, out _))
        {
            return tweetId;
        }

        var cleaned = url.Split('?', 2)[0].TrimEnd('/');
        var segments = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var last = segments.LastOrDefault() ?? string.Empty;
        return IsValidTweetId(last) ? last : string.Empty;
    }

    private static Func<string, string> ExtractString(JsonElement item)
    {
        return propertyName =>
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            if (!item.TryGetProperty(propertyName, out var property))
            {
                return string.Empty;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString() ?? string.Empty,
                JsonValueKind.Number => property.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty,
            };
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static bool MatchesTopic(SocialCommentDraft comment, string topic)
    {
        var source = Normalize($"{comment.Text} {comment.PostTitle} {comment.AuthorName} {comment.AuthorHandle}");
        return topic.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(term => Normalize(term).TrimEnd('s'))
            .Where(term => term.Length > 2)
            .All(source.Contains);
    }

    private static string Normalize(string value) =>
        value.Normalize(System.Text.NormalizationForm.FormD)
            .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Aggregate(new System.Text.StringBuilder(), (builder, character) => builder.Append(char.ToLowerInvariant(character)))
            .ToString();

    private static DateTimeOffset ParseTimestamp(params string[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (
                long.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var unix
                )
            )
            {
                if (unix > 1_000_000_000_000)
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(unix);
                }

                if (unix > 1_000_000_000)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unix);
                }
            }

            if (
                DateTimeOffset.TryParseExact(
                    value,
                    [
                        "ddd MMM dd HH:mm:ss zzzz yyyy",
                        "ddd MMM dd HH:mm:ss +0000 yyyy",
                        "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                        "yyyy-MM-dd'T'HH:mm:ss'Z'",
                        "yyyy-MM-dd'T'HH:mm:ss.fffffffzzz",
                        "yyyy-MM-dd'T'HH:mm:sszzz",
                    ],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                    out var exact
                )
            )
            {
                return exact.ToUniversalTime();
            }

            if (
                DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var parsed
                )
            )
            {
                return parsed;
            }
        }

        return DateTimeOffset.UtcNow;
    }

    private static string ExtractMentionHandle(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text[0] != '@')
        {
            return string.Empty;
        }

        var end = 1;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] is '_' or '.'))
        {
            end++;
        }

        return end > 1 ? text[1..end] : string.Empty;
    }

    private static string ExtractUsernameFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var segments = url.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (
                segments[index] is "x.com" or "twitter.com"
                && segments[index + 1] is not ("i" or "status")
            )
            {
                return segments[index + 1];
            }
        }

        return string.Empty;
    }
}
