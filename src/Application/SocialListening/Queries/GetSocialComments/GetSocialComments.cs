using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;

public record GetSocialCommentsQuery(
    string? Platform = null,
    bool Refresh = false,
    string? Topic = null,
    string? Prompt = null,
    IReadOnlyDictionary<string, string>? SourceUrls = null,
    string? Search = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Province = null,
    string? Canton = null,
    string? Parish = null
) : IQuery<SocialListeningDashboard>;

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

                var insights = SocialTextInsightsAnalyzer.Analyze(draft.Text);
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
                    sentiment,
                    draft.Province,
                    draft.Canton,
                    draft.Parish,
                    draft.Reactions,
                    insights,
                    SocialTextInsightsAnalyzer.IsLikelySpam(draft.Text)
                );
            })
            .Where(comment =>
                string.IsNullOrWhiteSpace(request.Platform)
                || comment.Platform.Equals(request.Platform, StringComparison.OrdinalIgnoreCase)
            )
            .Where(comment => request.From is null || comment.PublishedAt >= request.From)
            .Where(comment => request.To is null || comment.PublishedAt <= request.To)
            .Where(comment => MatchesSearch(comment, request.Search))
            .Where(comment => MatchesLocation(comment.Province, request.Province))
            .Where(comment => MatchesLocation(comment.Canton, request.Canton))
            .Where(comment => MatchesLocation(comment.Parish, request.Parish))
            .OrderByDescending(comment => comment.PublishedAt)
            .ToArray();

        var uniqueComments = comments
            .GroupBy(GetDeduplicationKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var duplicateCount = comments.Length - uniqueComments.Length;
        comments = uniqueComments;

        var repeatedTexts = comments
            .GroupBy(comment => Normalize(comment.Text), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var authorCounts = comments
            .GroupBy(
                comment => $"{comment.Platform}:{comment.AuthorHandle}",
                StringComparer.OrdinalIgnoreCase
            )
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.OrdinalIgnoreCase
            );
        var reviewedComments = comments
            .Select(comment =>
                comment with
                {
                    IsLikelyBot = SocialTextInsightsAnalyzer.IsLikelyAutomated(
                        authorCounts[$"{comment.Platform}:{comment.AuthorHandle}"],
                        repeatedTexts[Normalize(comment.Text)]
                    ),
                }
            )
            .ToArray();
        var suppressedCount = reviewedComments.Count(comment =>
            comment.IsLikelySpam || comment.IsLikelyBot
        );
        comments = reviewedComments
            .Where(comment => !comment.IsLikelySpam && !comment.IsLikelyBot)
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
            suppressedCount == 0 && duplicateCount == 0
                ? fetchResult.Message
                : string.Join(
                    " ",
                    new[]
                    {
                        fetchResult.Message,
                        suppressedCount > 0
                            ? $"Se excluyeron {suppressedCount} publicaciones con señales de spam o automatización."
                            : null,
                        duplicateCount > 0
                            ? $"Se eliminaron {duplicateCount} comentarios duplicados."
                            : null,
                    }.Where(message => !string.IsNullOrWhiteSpace(message))
                )
        );
    }

    private static bool MatchesSearch(SocialComment comment, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;
        var source = Normalize(
            $"{comment.AuthorName} {comment.AuthorHandle} {comment.Text} {comment.PostTitle}"
        );
        var tokens = System
            .Text.RegularExpressions.Regex.Matches(
                query,
                "\"([^\"]+)\"|\\b(AND|OR|NOT)\\b|[()]|[^\\s()]+",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            )
            .Select(match => match.Value.Trim('"'))
            .ToArray();
        var index = 0;
        bool Term()
        {
            if (index >= tokens.Length)
                throw new FormatException();
            if (tokens[index].Equals("NOT", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                return !Term();
            }
            if (tokens[index] == "(")
            {
                index++;
                var nested = Expression();
                if (index >= tokens.Length || tokens[index++] != ")")
                    throw new FormatException();
                return nested;
            }
            var value = tokens[index++];
            if (value is ")" or "AND" or "OR")
                throw new FormatException();
            return source.Contains(Normalize(value), StringComparison.Ordinal);
        }
        bool And()
        {
            var value = Term();
            while (
                index < tokens.Length
                && tokens[index].Equals("AND", StringComparison.OrdinalIgnoreCase)
            )
            {
                index++;
                var next = Term();
                value &= next;
            }
            return value;
        }
        bool Expression()
        {
            var value = And();
            while (
                index < tokens.Length
                && tokens[index].Equals("OR", StringComparison.OrdinalIgnoreCase)
            )
            {
                index++;
                var next = And();
                value |= next;
            }
            return value;
        }
        try
        {
            return tokens.Length == 0 || Expression() && index == tokens.Length;
        }
        catch (FormatException)
        {
            return source.Contains(Normalize(query), StringComparison.Ordinal);
        }
    }

    private static string Normalize(string value) =>
        value
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(character =>
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character)
                != System.Globalization.UnicodeCategory.NonSpacingMark
            )
            .Aggregate(
                new System.Text.StringBuilder(),
                (builder, character) => builder.Append(char.ToLowerInvariant(character))
            )
            .ToString();

    private static string GetDeduplicationKey(SocialComment comment)
    {
        if (Uri.TryCreate(comment.CommentUrl, UriKind.Absolute, out var commentUri))
        {
            var canonicalUri = new UriBuilder(commentUri)
            {
                Query = string.Empty,
                Fragment = string.Empty,
            }.Uri;
            return $"{comment.Platform}:{canonicalUri.AbsoluteUri}";
        }

        return $"{comment.Platform}:{comment.AuthorHandle}:{comment.PostUrl}:{comment.PublishedAt:O}:{Normalize(comment.Text)}";
    }

    private static bool MatchesLocation(string? actual, string? expected) =>
        string.IsNullOrWhiteSpace(expected)
        || (
            !string.IsNullOrWhiteSpace(actual)
            && Normalize(actual).Contains(Normalize(expected), StringComparison.Ordinal)
        );
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
    SentimentResult Sentiment = null!,
    string? Province = null,
    string? Canton = null,
    string? Parish = null,
    int? Reactions = null,
    SocialTextInsights? Insights = null,
    bool IsLikelySpam = false,
    bool IsLikelyBot = false
);

public record SentimentResult(string Label, string DisplayName, int Confidence);

public record SentimentSummary(int Total, int Positive, int Neutral, int Negative);
