using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.Common.Security;
using AnalisisSentimiento.Domain.Constants;

namespace AnalisisSentimiento.Application.Monitoring.Queries.GetScheduledMonitors;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Analyst},{Roles.Consulta}")]
public record GetScheduledMonitorsQuery : IQuery<IReadOnlyList<ScheduledMonitorDto>>;

internal class GetScheduledMonitorsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetScheduledMonitorsQuery, IReadOnlyList<ScheduledMonitorDto>>
{
    public async Task<IReadOnlyList<ScheduledMonitorDto>> Handle(
        GetScheduledMonitorsQuery request,
        CancellationToken cancellationToken
    )
    {
        return await context
            .ScheduledMonitors.AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .Select(x => new ScheduledMonitorDto(
                x.Id,
                x.Name,
                x.Topic,
                x.Platform,
                x.Province,
                x.Canton,
                x.Parish,
                x.IntervalMinutes,
                x.IsActive,
                x.LastRunAt,
                x.NextRunAt,
                x.LastNegativeRatio,
                x.LastTotal,
                x.LastPositive,
                x.LastNeutral,
                x.LastNegative,
                x.LastExecutiveSummary,
                x.OwnerUserName
            ))
            .ToListAsync(cancellationToken);
    }
}

public record ScheduledMonitorDto(
    int Id,
    string Name,
    string Topic,
    string? Platform,
    string? Province,
    string? Canton,
    string? Parish,
    int IntervalMinutes,
    bool IsActive,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? NextRunAt,
    double? LastNegativeRatio,
    int? LastTotal,
    int? LastPositive,
    int? LastNeutral,
    int? LastNegative,
    string? LastExecutiveSummary,
    string? OwnerUserName
);
