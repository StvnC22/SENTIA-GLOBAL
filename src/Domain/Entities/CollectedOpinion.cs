namespace AnalisisSentimiento.Domain.Entities;

public class CollectedOpinion : BaseAuditableEntity
{
    private CollectedOpinion() { }

    public string Platform { get; private set; } = string.Empty;
    public string ExternalId { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public string AuthorHandle { get; private set; } = string.Empty;
    public string AuthorUrl { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    public string CommentUrl { get; private set; } = string.Empty;
    public string PostTitle { get; private set; } = string.Empty;
    public string PostUrl { get; private set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; private set; }
    public DateTimeOffset CollectedAt { get; private set; }
    public string? Province { get; private set; }
    public string? Canton { get; private set; }
    public string? Parish { get; private set; }
    public int ReactionCount { get; private set; }
    public string SentimentLabel { get; private set; } = "neutral";
    public string SentimentDisplayName { get; private set; } = "Neutral";
    public int SentimentConfidence { get; private set; }

    public static CollectedOpinion Create(
        string platform,
        string externalId,
        string sourceUrl,
        DateTimeOffset collectedAt
    )
    {
        return new CollectedOpinion
        {
            Platform = platform.Trim().ToLowerInvariant(),
            ExternalId = externalId.Trim(),
            SourceUrl = sourceUrl.Trim(),
            CollectedAt = collectedAt,
        };
    }

    public void Update(
        string authorName,
        string authorHandle,
        string authorUrl,
        string text,
        string commentUrl,
        string postTitle,
        string postUrl,
        DateTimeOffset publishedAt,
        string? province,
        string? canton,
        string? parish,
        int reactionCount,
        string sentimentLabel,
        string sentimentDisplayName,
        int sentimentConfidence,
        DateTimeOffset collectedAt
    )
    {
        AuthorName = authorName;
        AuthorHandle = authorHandle;
        AuthorUrl = authorUrl;
        Text = text;
        CommentUrl = commentUrl;
        PostTitle = postTitle;
        PostUrl = postUrl;
        PublishedAt = publishedAt;
        Province = province;
        Canton = canton;
        Parish = parish;
        ReactionCount = Math.Max(0, reactionCount);
        SentimentLabel = sentimentLabel;
        SentimentDisplayName = sentimentDisplayName;
        SentimentConfidence = Math.Clamp(sentimentConfidence, 0, 100);
        CollectedAt = collectedAt;
    }
}
