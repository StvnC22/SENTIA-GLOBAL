using System.Net;
using System.Text;
using System.Text.Json;
using AnalisisSentimiento.Infrastructure.SocialListening.Apify;

namespace AnalisisSentimiento.Infrastructure.IntegrationTests.SocialListening;

public class ApifyClientTests
{
    [Test]
    public async Task ShouldReturnDatasetItemsFromActorRun()
    {
        var handler = new StubHttpMessageHandler(
            """
            [
              { "text": "Gran universidad", "ownerUsername": "maria" }
            ]
            """
        );

        var client = new ApifyClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.apify.com/v2/") }
        );

        var items = await client.RunActorSyncAsync(
            "apify/instagram-comment-scraper",
            new { directUrls = new[] { "https://example.com/post" } },
            "test-token",
            CancellationToken.None
        );

        items.Count.ShouldBe(1);
        items[0].GetProperty("text").GetString().ShouldBe("Gran universidad");
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            request.RequestUri!.AbsolutePath.ShouldContain("instagram-comment-scraper");
            request.RequestUri.Query.ShouldContain("token=test-token");

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };

            return Task.FromResult(response);
        }
    }
}
