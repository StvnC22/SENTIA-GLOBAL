namespace AnalisisSentimiento.Application.Common.Interfaces;

public interface ISocialCommentProvider
{
    Task<SocialCommentFetchResult> FetchAsync(
        string? platform,
        bool refresh,
        CancellationToken cancellationToken,
        string? topic = null,
        IReadOnlyDictionary<string, string>? sourceUrls = null
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
    DateTimeOffset PublishedAt
);
