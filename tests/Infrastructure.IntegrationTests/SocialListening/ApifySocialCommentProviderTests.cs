using System.Net;
using System.Text;
using System.Text.Json;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Infrastructure.SocialListening.Apify;
using AnalisisSentimiento.Infrastructure.SocialListening.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.IntegrationTests.SocialListening;

public class ApifySocialCommentProviderTests
{
    [Test]
    public async Task ShouldMergeXPostCommentsAndDirectReplies()
    {
        var handler = new QueueHttpMessageHandler()
            .AddActor(
                "igolaizola~x-twitter-scraper",
                """
            [
              {
                "id": "2031483366751556015",
                "text": "Publicación ULEAM",
                "permalink": "https://x.com/UleamEcuador/status/2031483366751556015",
                "createdAt": "2026-03-10T21:32:32Z"
              }
            ]
            """
            )
            .AddActor(
                "iron-crawler~twitter-comments",
                """
            [
              {
                "id": "2039999999999999999",
                "text": "Comentario bajo el post",
                "created_at": "Wed Sep 02 17:26:46 +0000 2026",
                "author": { "screen_name": "kuznetsov_52890", "name": "Oleg" }
              },
              {
                "id": "2040000000000000001",
                "in_reply_to_status_id_str": "2039999999999999999",
                "text": "Respuesta anidada",
                "created_at": "Wed Sep 02 18:00:00 +0000 2026",
                "author": { "screen_name": "ana_ec", "name": "Ana" }
              }
            ]
            """
            )
            .AddActor(
                "apidojo~tweet-scraper",
                """
            [
              {
                "id": "1738106896777699465",
                "url": "https://x.com/juan_ec/status/1738106896777699465",
                "text": "Mención directa a ULEAM",
                "createdAt": "Wed Jan 08 10:00:00 +0000 2025",
                "author": { "userName": "juan_ec", "name": "Juan" }
              }
            ]
            """
            );

        var provider = CreateProvider(handler);
        var result = await provider.FetchAsync("x", refresh: true, CancellationToken.None);

        result.Comments.Count.ShouldBe(3);
        result.Comments.Select(comment => comment.Text).ShouldContain("Comentario bajo el post");
        result.Comments.Select(comment => comment.Text).ShouldContain("Respuesta anidada");
        result.Comments.Select(comment => comment.Text).ShouldContain("Mención directa a ULEAM");
    }

    [Test]
    public async Task ShouldReturnEmptyWhenXHasNoPostsOrReplies()
    {
        var handler = new QueueHttpMessageHandler()
            .AddActor("igolaizola~x-twitter-scraper", "[]")
            .AddActor("apidojo~tweet-scraper", "[]", "[]");

        var provider = CreateProvider(handler);
        var result = await provider.FetchAsync("x", refresh: true, CancellationToken.None);

        result.DataSource.ShouldBe("demo");
        result.Comments.Count.ShouldBe(0);
    }

    [Test]
    public async Task ShouldMapXCommentsFromApifyActors()
    {
        var handler = new QueueHttpMessageHandler()
            .AddActor(
                "igolaizola~x-twitter-scraper",
                """
            [
              {
                "id": "2031483366751556015",
                "text": "ULEAM informa sobre nuevo proceso de admisiones",
                "permalink": "https://x.com/UleamEcuador/status/2031483366751556015",
                "createdAt": "2026-03-10T21:32:32Z"
              }
            ]
            """
            )
            .AddActor(
                "iron-crawler~twitter-comments",
                """
            [
              {
                "id": "2039999999999999999",
                "conversation_id": "2031483366751556015",
                "text": "Hmm... interesting",
                "created_at": "Wed Sep 02 17:26:46 +0000 2026",
                "author": {
                  "screen_name": "kuznetsov_52890",
                  "name": "Oleg"
                }
              }
            ]
            """
            )
            .AddActor("apidojo~tweet-scraper", "[]");

        var provider = CreateProvider(handler);
        var result = await provider.FetchAsync("x", refresh: true, CancellationToken.None);

        result.DataSource.ShouldBe("live");
        result.Comments.Count.ShouldBe(1);
        result.Comments.First().Text.ShouldBe("Hmm... interesting");
        result.Comments.First().AuthorHandle.ShouldBe("@kuznetsov_52890");
        result.Comments.First().AuthorName.ShouldBe("Oleg");
        result.Comments.First().PublishedAt.Year.ShouldBe(2026);
        result.Comments.First().PublishedAt.Month.ShouldBe(9);
        result.Comments.First().PublishedAt.Day.ShouldBe(2);
        result.Comments.First().Platform.ShouldBe("x");
    }

    [Test]
    public async Task ShouldMapTikTokCommentsFromVideos()
    {
        var handler = new QueueHttpMessageHandler(
            """
            [
              {
                "cid": "7399984975553086214",
                "uniqueId": "valec",
                "nickname": "Vale C.",
                "text": "La mejor universidad de Manabí",
                "createTimeISO": "2024-08-06T11:21:16.000Z",
                "videoWebUrl": "https://www.tiktok.com/@uleamecuador/video/7332342275151760642"
              }
            ]
            """
        );

        var provider = CreateProvider(handler, includeTikTok: true);
        var result = await provider.FetchAsync("tiktok", refresh: true, CancellationToken.None);

        result.DataSource.ShouldBe("live");
        result.Comments.Count.ShouldBe(1);
        result.Comments.First().AuthorHandle.ShouldBe("@valec");
        result.Comments.First().AuthorName.ShouldBe("Vale C.");
        result.Comments.First().Platform.ShouldBe("tiktok");
    }

    [Test]
    public async Task ShouldMapInstagramCommentsFromPosts()
    {
        var handler = new QueueHttpMessageHandler(
            """
            [
              {
                "id": "post-1",
                "url": "https://www.instagram.com/p/ABC123/",
                "caption": "Bienvenidos a ULEAM",
                "timestamp": "2026-03-01T12:00:00Z"
              }
            ]
            """,
            """
            [
              {
                "id": "ig-comment-1",
                "text": "Excelente universidad",
                "ownerUsername": "maria_ec",
                "ownerFullName": "María",
                "timestamp": "2026-03-02T10:00:00Z",
                "postUrl": "https://www.instagram.com/p/ABC123/"
              }
            ]
            """
        );

        var provider = CreateProvider(
            handler,
            profiles: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["instagram"] = "https://www.instagram.com/uleam_ecuador_oficial/",
            }
        );
        var result = await provider.FetchAsync("instagram", refresh: true, CancellationToken.None);

        result.DataSource.ShouldBe("live");
        result.Comments.Count.ShouldBe(1);
        result.Comments.First().Text.ShouldBe("Excelente universidad");
        result.Comments.First().AuthorHandle.ShouldBe("@maria_ec");
        result.Comments.First().Platform.ShouldBe("instagram");
    }

    [Test]
    public async Task ShouldMapFacebookCommentsFromPosts()
    {
        var handler = new QueueHttpMessageHandler(
            """
            [
              {
                "postId": "fb-post-1",
                "url": "https://www.facebook.com/UleamEc/posts/123",
                "text": "Proceso de admisiones abierto",
                "time": "2026-02-15T08:00:00Z"
              }
            ]
            """,
            """
            [
              {
                "id": "fb-comment-1",
                "text": "Gracias por la información",
                "profileName": "Carlos Pérez",
                "profileUrl": "https://www.facebook.com/carlos",
                "date": "2026-02-16T09:00:00Z",
                "postUrl": "https://www.facebook.com/UleamEc/posts/123"
              }
            ]
            """
        );

        var provider = CreateProvider(
            handler,
            profiles: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["facebook"] = "https://www.facebook.com/UleamEc",
            }
        );
        var result = await provider.FetchAsync("facebook", refresh: true, CancellationToken.None);

        result.DataSource.ShouldBe("live");
        result.Comments.Count.ShouldBe(1);
        result.Comments.First().Text.ShouldBe("Gracias por la información");
        result.Comments.First().AuthorName.ShouldBe("Carlos Pérez");
        result.Comments.First().Platform.ShouldBe("facebook");
    }

    private static ApifySocialCommentProvider CreateProvider(
        HttpMessageHandler handler,
        bool includeTikTok = false,
        Dictionary<string, string>? profiles = null
    )
    {
        var credentialStore = new StubCredentialStore("apify-token", includeTikTok ? "tiktok-token" : null);
        profiles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "https://x.com/UleamEcuador",
        };
        if (includeTikTok)
        {
            profiles["tiktok"] = "https://www.tiktok.com/@uleamecuador";
        }

        var options = Options.Create(
            new SocialListeningOptions
            {
                Profiles = profiles,
                Actors = new ApifyActorOptions
                {
                    InstagramPosts = "apify/instagram-scraper",
                    InstagramComments = "apify/instagram-comment-scraper",
                    FacebookPosts = "apify/facebook-posts-scraper",
                    FacebookComments = "apify/facebook-comments-scraper",
                    XPosts = "apidojo/tweet-scraper",
                    XTimeline = "igolaizola/x-twitter-scraper",
                    XComments = "iron-crawler/twitter-comments",
                    TikTokPosts = "clockworks/tiktok-scraper",
                    TikTokComments = "clockworks/tiktok-comments-scraper",
                },
            }
        );

        return new ApifySocialCommentProvider(
            new ApifyClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://api.apify.com/v2/") }
            ),
            credentialStore,
            options,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ApifySocialCommentProvider>.Instance
        );
    }

    private sealed class StubCredentialStore(string? xToken, string? tikTokToken = null)
        : IApiCredentialStore
    {
        public Task<ApiCredentials> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ApiCredentials(null, null, tikTokToken, xToken, null, null));

        public Task SaveAsync(ApiCredentials credentials, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new();
        private readonly Dictionary<string, Queue<string>> _responsesByActor = new(
            StringComparer.OrdinalIgnoreCase
        );

        public QueueHttpMessageHandler(params string[] responseBodies)
        {
            foreach (var body in responseBodies)
            {
                _responses.Enqueue(body);
            }
        }

        public QueueHttpMessageHandler AddActor(string actorKey, params string[] responseBodies)
        {
            _responsesByActor[actorKey] = new Queue<string>(responseBodies);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var path = request.RequestUri!.AbsolutePath;
            foreach (var (actorKey, queue) in _responsesByActor)
            {
                if (!path.Contains(actorKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (queue.Count == 0)
                {
                    throw new InvalidOperationException($"Sin respuesta para {actorKey}");
                }

                return Ok(queue.Dequeue());
            }

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("Sin respuesta HTTP configurada");
            }

            return Ok(_responses.Dequeue());
        }

        private static Task<HttpResponseMessage> Ok(string body) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                }
            );
    }
}
