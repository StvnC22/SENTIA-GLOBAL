using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.Common.Security;
using AnalisisSentimiento.Domain.Constants;

namespace AnalisisSentimiento.Application.Monitoring.Queries.GetActivityLog;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Analyst}")]
public record GetActivityLogQuery(int Take = 40) : IQuery<IReadOnlyList<ActivityLogDto>>;

internal class GetActivityLogQueryHandler(
    IApplicationDbContext context,
    IIdentityService identityService
) : IRequestHandler<GetActivityLogQuery, IReadOnlyList<ActivityLogDto>>
{
    public async Task<IReadOnlyList<ActivityLogDto>> Handle(
        GetActivityLogQuery request,
        CancellationToken cancellationToken
    )
    {
        var take = Math.Clamp(request.Take, 1, 200);
        var entries = await context
            .ActivityLogEntries.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var userId in entries.Select(x => x.UserName).OfType<string>().Distinct())
        {
            names[userId] = await identityService.GetUserNameAsync(userId) ?? userId;
        }

        return entries
            .Select(x => new ActivityLogDto(
                x.Id,
                x.OccurredAt,
                x.UserName is null ? null : names.GetValueOrDefault(x.UserName, x.UserName),
                x.Action,
                x.Detail
            ))
            .ToArray();
    }
}

public record ActivityLogDto(
    int Id,
    DateTimeOffset OccurredAt,
    string? UserName,
    string Action,
    string? Detail
);
