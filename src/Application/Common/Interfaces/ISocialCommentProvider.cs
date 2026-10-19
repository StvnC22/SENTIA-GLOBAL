namespace AnalisisSentimiento.Application.Common.Interfaces;

public interface ISocialCommentProvider
{
    Task<SocialCommentFetchResult> FetchAsync(
        string? platform,
        bool refresh,
        CancellationToken cancellationToken,
        string? topic = null,
        IReadOnlyDictionary<string, string>? sourceUrls = null,
        string? search = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null
    );
}

public sealed record SocialCommentFetchResult(
    string DataSource,
    IReadOnlyList<SocialCommentDraft> Comments,
    string? Message = null
)
{
    public static SocialCommentFetchResult Demo(
        IReadOnlyList<SocialCommentDraft> comments,
        string? message = null
    ) => new("demo", comments, message);

    public static SocialCommentFetchResult Live(IReadOnlyList<SocialCommentDraft> comments) =>
        new("live", comments);
}

public sealed record SocialCommentDraft(
    string Id,
    string Platform,
    string AuthorName,
    string AuthorHandle,
    string AuthorUrl,
    string Text,
    string CommentUrl,
    string PostTitle,
    string PostUrl,
    DateTimeOffset PublishedAt,
    string? Province = null,
    string? Canton = null,
    string? Parish = null,
    int? Reactions = null,
    string? PlaceName = null,
    string? RegionCode = null,
    string ContentType = "comment"
);

public interface ISocialMonitorStore
{
    Task<IReadOnlyList<SocialMonitorSubscription>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SocialMonitorSubscription>> GetForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken
    );

    Task SaveAsync(SocialMonitorSubscription subscription, CancellationToken cancellationToken);

    Task DeleteAsync(string id, string ownerId, CancellationToken cancellationToken);
}

public sealed record SocialMonitorSubscription(
    string Id,
    string OwnerId,
    bool Enabled,
    int IntervalMinutes,
    int AlertThreshold,
    string? Platform,
    string? Topic,
    string? Search,
    IReadOnlyDictionary<string, string> SourceUrls,
    DateTimeOffset NextRunAt,
    int? PreviousNegativeRate = null,
    string? LastAlert = null,
    DateTimeOffset? LastRunAt = null,
    string? LastError = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Province = null,
    string? Canton = null,
    string? Parish = null
);

public interface IAuditActivityStore
{
    Task AppendAsync(AuditActivity activity, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditActivity>> GetRecentAsync(
        int count,
        CancellationToken cancellationToken
    );
}

public sealed record AuditActivity(
    DateTimeOffset OccurredAt,
    string ActorId,
    string Method,
    string Path,
    int StatusCode
);
