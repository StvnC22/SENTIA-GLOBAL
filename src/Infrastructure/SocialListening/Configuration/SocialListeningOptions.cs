namespace AnalisisSentimiento.Infrastructure.SocialListening.Configuration;

public sealed class SocialListeningOptions
{
    public const string SectionName = "SocialListening";

    public Dictionary<string, string> Profiles { get; init; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // These targets are intentionally configurable. Override them in appsettings.Development.json
            // or environment variables for the organization, campaign or topic being monitored.
            ["instagram"] = "",
            ["facebook"] = "",
            ["tiktok"] = "",
            ["x"] = "",
        };

    public int MaxPostsPerProfile { get; init; } = 8;

    /// <summary>Instagram necesita más posts para cubrir periodos largos (p. ej. 1 año).</summary>
    public int InstagramMaxPosts { get; init; } = 60;

    /// <summary>Facebook necesita más posts para cubrir periodos largos (p. ej. 1 año).</summary>
    public int FacebookMaxPosts { get; init; } = 60;

    public int MaxCommentsPerPost { get; init; } = 20;

    /// <summary>Comentarios por publicación de Instagram.</summary>
    public int InstagramMaxCommentsPerPost { get; init; } = 50;

    /// <summary>Comentarios por publicación de Facebook (las páginas activas suelen tener muchos).</summary>
    public int FacebookMaxCommentsPerPost { get; init; } = 50;

    /// <summary>Límite por red para que Instagram/Facebook/TikTok/X no se pisen entre sí.</summary>
    public int MaxCommentsPerPlatform { get; init; } = 250;

    /// <summary>Tope global opcional; se aplica solo después de respetar el cupo por red.</summary>
    public int MaxTotalComments { get; init; } = 600;

    /// <summary>Memoria solicitada a Apify por ejecución. Un valor bajo reduce el consumo del plan gratuito.</summary>
    public int ApifyMemoryMb { get; init; } = 1024;

    /// <summary>Posts de X a revisar (recientes y anteriores) para sacar comentarios.</summary>
    public int XMaxPosts { get; init; } = 15;

    /// <summary>Páginas de comentarios por post en el actor de X.</summary>
    public int XMaxCommentPages { get; init; } = 3;

    /// <summary>Posts recientes de X con paginación extra de comentarios.</summary>
    public int XRecentPostsBoost { get; init; } = 6;

    /// <summary>URLs de publicación por llamada al actor de comentarios de IG/FB.</summary>
    public int CommentUrlBatchSize { get; init; } = 12;

    public int CacheMinutes { get; init; } = 60;

    public ApifyActorOptions Actors { get; init; } = new();

    public GeminiOptions Gemini { get; init; } = new();
}

public sealed class ApifyActorOptions
{
    public string InstagramPosts { get; init; } = "apify/instagram-scraper";

    public string InstagramComments { get; init; } = "apify/instagram-comment-scraper";

    public string FacebookPosts { get; init; } = "apify/facebook-posts-scraper";

    public string FacebookComments { get; init; } = "apify/facebook-comments-scraper";

    public string TikTokPosts { get; init; } = "clockworks/tiktok-scraper";

    public string TikTokComments { get; init; } = "clockworks/tiktok-comments-scraper";

    public string XPosts { get; init; } = "apidojo/tweet-scraper";

    public string XTimeline { get; init; } = "igolaizola/x-twitter-scraper";

    public string XComments { get; init; } = "iron-crawler/twitter-comments";

    public string ProfileComments { get; init; } = "tri_angle/social-media-sentiment-analysis-tool";
}

public sealed class GeminiOptions
{
    public string Model { get; init; } = "gemini-2.5-flash";
}
