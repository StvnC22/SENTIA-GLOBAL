using System.Net;
using System.Text;
using System.Text.Json;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Infrastructure.SocialListening.Apify;
using AnalisisSentimiento.Infrastructure.SocialListening.Configuration;
using AnalisisSentimiento.Infrastructure.SocialListening.Sentiment;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.IntegrationTests.SocialListening;

public class GeminiSentimentAnalyzerTests
{
    [Test]
    public async Task ShouldClassifyCommentsWithGeminiResponse()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      {
                        "text": "[{\"id\":\"c1\",\"label\":\"positive\",\"displayName\":\"Positivo\",\"confidence\":92}]"
                      }
                    ]
                  }
                }
              ]
            }
            """
        );

        var analyzer = CreateAnalyzer(handler);

        var results = await analyzer.AnalyzeBatchAsync(
            [new SentimentAnalysisRequest("c1", "Excelente universidad")],
            CancellationToken.None
        );

        results["c1"].Label.ShouldBe("positive");
        results["c1"].DisplayName.ShouldBe("Positivo");
        results["c1"].Confidence.ShouldBe(92);
    }

    [Test]
    public async Task ShouldFallbackToLocalAnalyzerWhenGeminiKeyIsMissing()
    {
        var handler = new StubHttpMessageHandler("{}");
        var analyzer = CreateAnalyzer(handler, includeGeminiKey: false);

        var results = await analyzer.AnalyzeBatchAsync(
            [new SentimentAnalysisRequest("c1", "Excelente universidad")],
            CancellationToken.None
        );

        results["c1"].Label.ShouldBe("positive");
    }

    private static GeminiSentimentAnalyzer CreateAnalyzer(
        HttpMessageHandler handler,
        bool includeGeminiKey = true
    )
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
        };

        var credentialStore = new StubCredentialStore(includeGeminiKey ? "test-gemini-key" : null);

        return new GeminiSentimentAnalyzer(
            httpClient,
            credentialStore,
            Options.Create(new SocialListeningOptions()),
            new LocalSentimentAnalyzer(),
            NullLogger<GeminiSentimentAnalyzer>.Instance
        );
    }

    private sealed class StubCredentialStore(string? geminiKey) : IApiCredentialStore
    {
        public Task<ApiCredentials> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(
                new ApiCredentials(null, null, null, null, geminiKey, DateTimeOffset.UtcNow)
            );

        public Task SaveAsync(ApiCredentials credentials, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };

            return Task.FromResult(response);
        }
    }
}
