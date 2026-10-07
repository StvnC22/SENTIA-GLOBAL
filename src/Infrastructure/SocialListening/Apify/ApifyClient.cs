using System.Net.Http.Json;
using System.Text.Json;
using AnalisisSentimiento.Infrastructure.SocialListening.Configuration;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.SocialListening.Apify;

public sealed class ApifyClient
{
    private readonly HttpClient httpClient;
    private readonly IOptions<SocialListeningOptions> options;

    // The optional options parameter keeps direct integrations/tests compatible
    // while leaving only one constructor for ASP.NET typed-client activation.
    public ApifyClient(HttpClient httpClient, IOptions<SocialListeningOptions>? options = null)
    {
        this.httpClient = httpClient;
        this.options = options ?? Options.Create(new SocialListeningOptions());
    }

    public async Task<IReadOnlyList<JsonElement>> RunActorSyncAsync(
        string actorId,
        object input,
        string apiToken,
        CancellationToken cancellationToken
    )
    {
        var actorPath = actorId.Replace('/', '~');
        var memoryMb = Math.Clamp(options.Value.ApifyMemoryMb, 512, 4096);
        var requestUri =
            $"acts/{actorPath}/run-sync-get-dataset-items?token={Uri.EscapeDataString(apiToken)}&memory={memoryMb}";

        using var response = await httpClient.PostAsJsonAsync(requestUri, input, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Apify respondió {(int)response.StatusCode} para {actorId}: {errorBody}",
                null,
                response.StatusCode
            );
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken
        );

        if (payload.ValueKind == JsonValueKind.Array)
        {
            return payload.EnumerateArray().ToArray();
        }

        if (
            payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array
        )
        {
            return items.EnumerateArray().ToArray();
        }

        return [];
    }
}
