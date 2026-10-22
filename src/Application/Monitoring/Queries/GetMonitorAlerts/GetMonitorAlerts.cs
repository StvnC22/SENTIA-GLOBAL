using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.Common.Security;
using AnalisisSentimiento.Domain.Constants;

namespace AnalisisSentimiento.Application.Monitoring.Queries.GetMonitorAlerts;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Analyst},{Roles.Consulta}")]
public record GetMonitorAlertsQuery(bool UnreadOnly = false)
    : IQuery<IReadOnlyList<MonitorAlertDto>>;

internal class GetMonitorAlertsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMonitorAlertsQuery, IReadOnlyList<MonitorAlertDto>>
{
    public async Task<IReadOnlyList<MonitorAlertDto>> Handle(
        GetMonitorAlertsQuery request,
        CancellationToken cancellationToken
    )
    {
        var query = context.MonitorAlerts.AsNoTracking().AsQueryable();
        if (request.UnreadOnly)
        {
            query = query.Where(x => !x.IsRead);
        }

        return await query
            .OrderByDescending(x => x.Id)
            .Take(50)
            .Select(x => new MonitorAlertDto(
                x.Id,
                x.ScheduledMonitorId,
                x.MonitorName,
                x.Message,
                x.PreviousNegativeRatio,
                x.CurrentNegativeRatio,
                x.IsRead,
                x.Created
            ))
            .ToListAsync(cancellationToken);
    }
}

public record MonitorAlertDto(
    int Id,
    int ScheduledMonitorId,
    string MonitorName,
    string Message,
    double PreviousNegativeRatio,
    double CurrentNegativeRatio,
    bool IsRead,
    DateTimeOffset Created
);
