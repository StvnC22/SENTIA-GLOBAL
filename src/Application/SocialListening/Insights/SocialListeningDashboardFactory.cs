using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;

namespace AnalisisSentimiento.Application.SocialListening.Insights;

public static class SocialListeningDashboardFactory
{
    public static SocialListeningDashboard Create(
        IReadOnlyList<SocialComment> comments,
        string? topic,
        DateTimeOffset updatedAt,
        string dataSource,
        string? message
    )
    {
        var summary = new SentimentSummary(
            comments.Count,
            comments.Count(comment => comment.Sentiment.Label == "positive"),
            comments.Count(comment => comment.Sentiment.Label == "neutral"),
            comments.Count(comment => comment.Sentiment.Label == "negative")
        );

        return new SocialListeningDashboard(
            comments,
            summary,
            updatedAt,
            dataSource,
            message,
            DashboardInsightBuilder.Build(comments, topic)
        );
    }
}
