using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;

public record GetSocialCommentsQuery(
    string? Platform = null,
    bool Refresh = false,
    string? Topic = null,
    string? Prompt = null,
    IReadOnlyDictionary<string, string>? SourceUrls = null
)
    : IQuery<SocialListeningDashboard>;

internal class GetSocialCommentsQueryHandler(
    ISocialCommentProvider commentProvider,
    ISentimentAnalyzer sentimentAnalyzer,
    TimeProvider timeProvider
) : IRequestHandler<GetSocialCommentsQuery, SocialListeningDashboard>
{
    public async Task<SocialListeningDashboard> Handle(
        GetSocialCommentsQuery request,
        CancellationToken cancellationToken
    )
    {
        var fetchResult = await commentProvider.FetchAsync(
            request.Platform,
            request.Refresh,
            cancellationToken,
            request.Topic,
            request.SourceUrls
        );
        var drafts = fetchResult.Comments;

        var sentimentRequests = drafts
            .Select(comment => new SentimentAnalysisRequest(comment.Id, comment.Text))
            .ToArray();

        var sentiments = await sentimentAnalyzer.AnalyzeBatchAsync(
            sentimentRequests,
            cancellationToken,
            request.Prompt
        );

        var comments = drafts
            .Select(draft =>
            {
                var sentiment = sentiments.TryGetValue(draft.Id, out var analyzed)
                    ? analyzed
                    : SpanishSentimentAnalyzer.Analyze(draft.Text);

                return new SocialComment(
                    draft.Id,
                    draft.Platform,
                    draft.AuthorName,
                    draft.AuthorHandle,
                    draft.AuthorUrl,
                    draft.Text,
                    draft.CommentUrl,
                    draft.PostTitle,
                    draft.PostUrl,
                    draft.PublishedAt,
                    sentiment
                );
            })
            .Where(comment =>
                string.IsNullOrWhiteSpace(request.Platform)
                || comment.Platform.Equals(request.Platform, StringComparison.OrdinalIgnoreCase)
            )
            .OrderByDescending(comment => comment.PublishedAt)
            .ToArray();

        var summary = new SentimentSummary(
            comments.Length,
            comments.Count(comment => comment.Sentiment.Label == "positive"),
            comments.Count(comment => comment.Sentiment.Label == "neutral"),
            comments.Count(comment => comment.Sentiment.Label == "negative")
        );

        return new SocialListeningDashboard(
            comments,
            summary,
            timeProvider.GetUtcNow(),
            fetchResult.DataSource,
            fetchResult.Message
        );
    }
}

public static class SpanishSentimentAnalyzer
{
    private static readonly string[] PositiveTerms =
    [
        "excelente",
        "gracias",
        "gran",
        "lindo",
        "linda",
        "mejor",
        "orgullo",
        "orgulloso",
        "orgullosa",
        "apoyar",
        "apoyo",
        "felicit",
        "maravilloso",
        "hermoso",
        "éxito",
        "exito",
        "bienvenido",
        "bienvenida",
        "clara",
        "amor",
        "bravo",
        "increíble",
        "increible",
    ];

    private static readonly string[] NegativeTerms =
    [
        "imposible",
        "mala",
        "malo",
        "malísimo",
        "no funciona",
        "no responden",
        "otra vez",
        "pésame",
        "pesame",
        "condolencia",
        "pérdida",
        "perdida",
        "muerte",
        "falleci",
        "triste",
        "dolor",
        "qdd",
        "q.e.p.d",
        "qepd",
        "descansa",
        "paz en",
        "vergüenza",
        "verguenza",
        "denuncia",
        "corrupci",
        "pésima",
        "pesima",
    ];

    public static SentimentResult Analyze(string text)
    {
        var normalizedText = text.ToLowerInvariant();
        var passiveAggressiveSignals = new[]
        {
            "gracias por nada",
            "qué sorpresa",
            "que sorpresa",
            "como siempre",
            "de mal en peor",
            "muy bien, pero",
            "lamentable que",
            "lamentable esto",
            "no confirmaron",
            "no respondieron",
            "no las entregaron",
            "no lo entregaron",
            "no entregaron",
            "no me dieron",
            "no nos dieron",
            "no recibí",
            "no recibimos",
            "no llegó",
            "no llegaron",
            "pedido incompleto",
            "faltaron",
            "ni siquiera",
            "más nada",
        };
        if (passiveAggressiveSignals.Any(normalizedText.Contains))
        {
            return new SentimentResult("negative", "Negativo", 84);
        }
        var positiveScore = PositiveTerms.Count(normalizedText.Contains);
        var negativeScore = NegativeTerms.Count(normalizedText.Contains);

        if (positiveScore > negativeScore)
        {
            return new SentimentResult(
                "positive",
                "Positivo",
                Math.Min(96, 78 + positiveScore * 6)
            );
        }

        if (negativeScore > positiveScore)
        {
            return new SentimentResult(
                "negative",
                "Negativo",
                Math.Min(96, 78 + negativeScore * 6)
            );
        }

        return new SentimentResult("neutral", "Neutral", 74);
    }
}

public record SocialListeningDashboard(
    IReadOnlyCollection<SocialComment> Comments,
    SentimentSummary Summary,
    DateTimeOffset UpdatedAt,
    string DataSource = "demo",
    string? Message = null
);

public record SocialComment(
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
    SentimentResult Sentiment = null!
);

public record SentimentResult(string Label, string DisplayName, int Confidence);

public record SentimentSummary(int Total, int Positive, int Neutral, int Negative);
