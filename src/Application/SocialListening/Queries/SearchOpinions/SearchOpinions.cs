using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.SocialListening.Insights;
using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;
using AnalisisSentimiento.Application.SocialListening.Search;
using AnalisisSentimiento.Application.SocialListening.Territory;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.SocialListening.Queries.SearchOpinions;

public record SearchOpinionsQuery(
    string? Topic = null,
    string? Platform = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    string? Province = null,
    string? Canton = null,
    string? Parish = null
) : IQuery<SocialListeningDashboard>;

internal class SearchOpinionsQueryHandler(
    IApplicationDbContext context,
    ISender sender,
    TimeProvider timeProvider
) : IRequestHandler<SearchOpinionsQuery, SocialListeningDashboard>
{
    public async Task<SocialListeningDashboard> Handle(
        SearchOpinionsQuery request,
        CancellationToken cancellationToken
    )
    {
        if (!await context.CollectedOpinions.AnyAsync(cancellationToken))
        {
            var demo = await sender.Send(
                new GetSocialCommentsQuery(
                    Platform: request.Platform,
                    Topic: request.Topic,
                    FromDate: request.FromDate,
                    ToDate: request.ToDate,
                    Province: request.Province,
                    Canton: request.Canton,
                    Parish: request.Parish
                ),
                cancellationToken
            );
            return demo with
            {
                Message =
                    "Aún no hay opiniones recolectadas. Recolecte desde los módulos de cada red (Instagram, Facebook, TikTok o X); mientras tanto se muestran datos demostrativos.",
            };
        }

        var platform = request.Platform?.Trim().ToLowerInvariant();
        var query = context.CollectedOpinions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(platform))
        {
            query = query.Where(opinion => opinion.Platform == platform);
        }

        var opinions = await query.ToListAsync(cancellationToken);
        var comments = opinions
            .Select(ToSocialComment)
            .Where(comment =>
                BooleanQueryMatcher.Matches(
                    request.Topic,
                    $"{comment.Text} {comment.PostTitle} {comment.AuthorName} {comment.AuthorHandle}"
                )
            )
            .Where(comment =>
                ManabiTerritoryCatalog.MatchesFilter(
                    comment.Province,
                    comment.Canton,
                    comment.Parish,
                    request.Province,
                    request.Canton,
                    request.Parish
                )
            )
            .Where(comment =>
                (request.FromDate is null || comment.PublishedAt >= request.FromDate)
                && (request.ToDate is null || comment.PublishedAt <= request.ToDate)
            )
            .OrderByDescending(comment => comment.PublishedAt)
            .ToArray();

        var message =
            comments.Length == 0
                ? $"Ninguna de las {opinions.Count} opiniones recolectadas coincide con la búsqueda y los filtros."
                : null;

        return SocialListeningDashboardFactory.Create(
            comments,
            request.Topic,
            timeProvider.GetUtcNow(),
            "live",
            message
        );
    }

    private static SocialComment ToSocialComment(CollectedOpinion opinion)
    {
        var (province, canton, parish) = ResolveTerritory(opinion);
        var enrichment = TextEnrichment.Extract(opinion.Text, opinion.SentimentLabel);

        return new SocialComment(
            opinion.ExternalId,
            opinion.Platform,
            opinion.AuthorName,
            opinion.AuthorHandle,
            opinion.AuthorUrl,
            opinion.Text,
            opinion.CommentUrl,
            opinion.PostTitle,
            opinion.PostUrl,
            opinion.PublishedAt,
            new SentimentResult(
                opinion.SentimentLabel,
                opinion.SentimentDisplayName,
                opinion.SentimentConfidence
            ),
            province,
            canton,
            parish,
            opinion.ReactionCount,
            enrichment.Topics,
            enrichment.Entities,
            enrichment.Emotions,
            enrichment.Aspects
        );
    }

    private static (string? Province, string? Canton, string? Parish) ResolveTerritory(
        CollectedOpinion opinion
    )
    {
        if (
            !string.IsNullOrWhiteSpace(opinion.Province)
            || !string.IsNullOrWhiteSpace(opinion.Canton)
        )
        {
            return (opinion.Province, opinion.Canton, opinion.Parish);
        }

        var inferred = ManabiTerritoryCatalog.InferFromText($"{opinion.Text} {opinion.PostTitle}");
        return inferred is null
            ? (null, null, null)
            : (inferred.Province, inferred.Canton, inferred.Parish);
    }
}
