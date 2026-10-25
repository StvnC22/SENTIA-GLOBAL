using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.Common.Security;
using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.SocialListening.Commands.CollectSocialComments;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Analyst}")]
public record CollectSocialCommentsCommand(
    string Platform,
    string SourceUrl,
    bool Refresh = false,
    string? Prompt = null
) : ICommand<SocialListeningDashboard>;

public class CollectSocialCommentsCommandValidator : AbstractValidator<CollectSocialCommentsCommand>
{
    public CollectSocialCommentsCommandValidator()
    {
        RuleFor(x => x.Platform)
            .NotEmpty()
            .Must(platform =>
                OpinionCollector.Platforms.Contains(platform.Trim().ToLowerInvariant())
            )
            .WithMessage("Red social no soportada. Use instagram, facebook, tiktok o x.");
        RuleFor(x => x.SourceUrl).NotEmpty().MaximumLength(1000);
    }
}

internal class CollectSocialCommentsCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    IUser user,
    TimeProvider timeProvider
) : IRequestHandler<CollectSocialCommentsCommand, Result<SocialListeningDashboard>>
{
    public async Task<Result<SocialListeningDashboard>> Handle(
        CollectSocialCommentsCommand request,
        CancellationToken cancellationToken
    )
    {
        var (dashboard, stored) = await OpinionCollector.CollectAsync(
            context,
            sender,
            request.Platform,
            request.SourceUrl,
            request.Refresh,
            request.Prompt,
            timeProvider.GetUtcNow(),
            cancellationToken
        );

        context.ActivityLogEntries.Add(
            ActivityLogEntry.Create(
                "opinions.collected",
                user.Id,
                $"{request.Platform}: {dashboard.Summary.Total} opiniones · {stored} nuevas o actualizadas en el corpus"
            )
        );
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok(dashboard);
    }
}

internal static class OpinionCollector
{
    public static readonly string[] Platforms = ["instagram", "facebook", "tiktok", "x"];

    public static async Task<(SocialListeningDashboard Dashboard, int Stored)> CollectAsync(
        IApplicationDbContext context,
        ISender sender,
        string platform,
        string sourceUrl,
        bool refresh,
        string? prompt,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var normalizedPlatform = platform.Trim().ToLowerInvariant();
        var dashboard = await sender.Send(
            new GetSocialCommentsQuery(
                Platform: normalizedPlatform,
                Refresh: refresh,
                Prompt: prompt,
                SourceUrls: new Dictionary<string, string> { [normalizedPlatform] = sourceUrl }
            ),
            cancellationToken
        );

        if (dashboard.DataSource != "live" || dashboard.Comments.Count == 0)
        {
            return (dashboard, 0);
        }

        var ids = dashboard.Comments.Select(comment => comment.Id).Distinct().ToArray();
        var existing = await context
            .CollectedOpinions.Where(opinion =>
                opinion.Platform == normalizedPlatform && ids.Contains(opinion.ExternalId)
            )
            .ToDictionaryAsync(opinion => opinion.ExternalId, cancellationToken);

        foreach (var comment in dashboard.Comments.DistinctBy(comment => comment.Id))
        {
            if (!existing.TryGetValue(comment.Id, out var opinion))
            {
                opinion = CollectedOpinion.Create(normalizedPlatform, comment.Id, sourceUrl, now);
                context.CollectedOpinions.Add(opinion);
                existing[comment.Id] = opinion;
            }

            opinion.Update(
                comment.AuthorName,
                comment.AuthorHandle,
                comment.AuthorUrl,
                comment.Text,
                comment.CommentUrl,
                comment.PostTitle,
                comment.PostUrl,
                comment.PublishedAt,
                comment.Province,
                comment.Canton,
                comment.Parish,
                comment.ReactionCount,
                comment.Sentiment.Label,
                comment.Sentiment.DisplayName,
                comment.Sentiment.Confidence,
                now
            );
        }

        return (dashboard, dashboard.Comments.Count);
    }
}
