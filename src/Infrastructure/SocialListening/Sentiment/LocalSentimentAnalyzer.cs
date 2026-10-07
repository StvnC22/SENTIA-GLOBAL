using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;

namespace AnalisisSentimiento.Infrastructure.SocialListening.Sentiment;

public sealed class LocalSentimentAnalyzer : ISentimentAnalyzer
{
    public Task<IReadOnlyDictionary<string, SentimentResult>> AnalyzeBatchAsync(
        IReadOnlyList<SentimentAnalysisRequest> comments,
        CancellationToken cancellationToken,
        string? prompt = null
    )
    {
        var results = comments.ToDictionary(
            comment => comment.Id,
            comment => SpanishSentimentAnalyzer.Analyze(comment.Text)
        );

        return Task.FromResult<IReadOnlyDictionary<string, SentimentResult>>(results);
    }
}
