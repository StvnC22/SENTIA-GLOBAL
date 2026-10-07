using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;

namespace AnalisisSentimiento.Application.Common.Interfaces;

public interface ISentimentAnalyzer
{
    Task<IReadOnlyDictionary<string, SentimentResult>> AnalyzeBatchAsync(
        IReadOnlyList<SentimentAnalysisRequest> comments,
        CancellationToken cancellationToken,
        string? prompt = null
    );
}

public sealed record SentimentAnalysisRequest(string Id, string Text);
