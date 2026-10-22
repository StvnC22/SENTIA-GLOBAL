using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.Common.Security;
using AnalisisSentimiento.Application.Monitoring.Commands.ExecuteDueMonitors;
using AnalisisSentimiento.Application.Monitoring.Queries.GetScheduledMonitors;
using AnalisisSentimiento.Domain.Constants;

namespace AnalisisSentimiento.Application.Monitoring.Commands.RunScheduledMonitorNow;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Analyst}")]
public record RunScheduledMonitorNowCommand(int Id) : ICommand<ScheduledMonitorDto>;

internal class RunScheduledMonitorNowCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    TimeProvider timeProvider
) : IRequestHandler<RunScheduledMonitorNowCommand, Result<ScheduledMonitorDto>>
{
    public async Task<Result<ScheduledMonitorDto>> Handle(
        RunScheduledMonitorNowCommand request,
        CancellationToken cancellationToken
    )
    {
        var monitor = await context.ScheduledMonitors.FindAsync([request.Id], cancellationToken);
        if (monitor is null)
        {
            return Result.Fail(new NotFoundError($"Monitoreo {request.Id} no encontrado."));
        }

        await MonitorExecution.ExecuteAsync(
            context,
            sender,
            monitor,
            timeProvider.GetUtcNow(),
            cancellationToken
        );

        return Result.Ok(
            new ScheduledMonitorDto(
                monitor.Id,
                monitor.Name,
                monitor.Topic,
                monitor.Platform,
                monitor.Province,
                monitor.Canton,
                monitor.Parish,
                monitor.IntervalMinutes,
                monitor.IsActive,
                monitor.LastRunAt,
                monitor.NextRunAt,
                monitor.LastNegativeRatio,
                monitor.LastTotal,
                monitor.LastPositive,
                monitor.LastNeutral,
                monitor.LastNegative,
                monitor.LastExecutiveSummary,
                monitor.OwnerUserName
            )
        );
    }
}
