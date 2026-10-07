using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AnalisisSentimiento.Web.Endpoints;

public class SocialComments : IEndpointGroup
{
    public static string RoutePrefix => "/api/social-comments";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetSocialComments);
    }

    [EndpointSummary("Obtener comentarios y análisis de sentimiento")]
    [EndpointDescription("Devuelve comentarios sociales clasificados por sentimiento.")]
    public static async Task<Ok<SocialListeningDashboard>> GetSocialComments(
        ISender sender,
        string? platform,
        bool refresh = false,
        string? topic = null,
        string? prompt = null,
        string? instagramUrl = null,
        string? facebookUrl = null,
        string? tiktokUrl = null,
        string? xUrl = null
    )
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddSource(sources, "instagram", instagramUrl);
        AddSource(sources, "facebook", facebookUrl);
        AddSource(sources, "tiktok", tiktokUrl);
        AddSource(sources, "x", xUrl);
        var dashboard = await sender.Send(new GetSocialCommentsQuery(platform, refresh, topic, prompt, sources));
        return TypedResults.Ok(dashboard);
    }

    private static void AddSource(IDictionary<string, string> sources, string platform, string? url)
    {
        if (!string.IsNullOrWhiteSpace(url)) sources[platform] = url.Trim();
    }
}
