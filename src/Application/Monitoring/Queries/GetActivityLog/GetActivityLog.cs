using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.Common.Security;
using AnalisisSentimiento.Domain.Constants;

namespace AnalisisSentimiento.Application.Monitoring.Queries.GetActivityLog;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Analyst}")]
public record GetActivityLogQuery(int Take = 40) : IQuery<IReadOnlyList<ActivityLogDto>>;

internal class GetActivityLogQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetActivityLogQuery, IReadOnlyList<ActivityLogDto>>
{
    public async Task<IReadOnlyList<ActivityLogDto>> Handle(
        GetActivityLogQuery request,
        CancellationToken cancellationToken
    )
    {
        var take = Math.Clamp(request.Take, 1, 100);
        return await context
            .ActivityLogEntries.AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .Take(take)
            .Select(x => new ActivityLogDto(x.Id, x.OccurredAt, x.UserName, x.Action, x.Detail))
            .ToListAsync(cancellationToken);
    }
}

public record ActivityLogDto(
    int Id,
    DateTimeOffset OccurredAt,
    string? UserName,
    string Action,
    string? Detail
);
