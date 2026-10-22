using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.Monitoring.Commands.ExecuteDueMonitors;

public record ExecuteDueMonitorsCommand(int MaxMonitors = 10) : ICommand<int>;

internal class ExecuteDueMonitorsCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    TimeProvider timeProvider
) : IRequestHandler<ExecuteDueMonitorsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        ExecuteDueMonitorsCommand request,
        CancellationToken cancellationToken
    )
    {
        var now = timeProvider.GetUtcNow();
        var dueMonitors = (
            await context.ScheduledMonitors.Where(x => x.IsActive).ToListAsync(cancellationToken)
        )
            .Where(x => x.NextRunAt is null || x.NextRunAt <= now)
            .OrderBy(x => x.NextRunAt)
            .Take(Math.Clamp(request.MaxMonitors, 1, 50))
            .ToArray();

        foreach (var monitor in dueMonitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await MonitorExecution.ExecuteAsync(context, sender, monitor, now, cancellationToken);
        }

        return Result.Ok(dueMonitors.Length);
    }
}

internal static class MonitorExecution
{
    public static async Task ExecuteAsync(
        IApplicationDbContext context,
        ISender sender,
        ScheduledMonitor monitor,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var dashboard = await sender.Send(
                new GetSocialCommentsQuery(
                    Platform: monitor.Platform,
                    Refresh: true,
                    Topic: monitor.Topic,
                    Province: monitor.Province,
                    Canton: monitor.Canton,
                    Parish: monitor.Parish
                ),
                cancellationToken
            );

            var alert = monitor.RecordRun(
                now,
                dashboard.Summary.Total,
                dashboard.Summary.Positive,
                dashboard.Summary.Neutral,
                dashboard.Summary.Negative,
                dashboard.Insights?.ExecutiveSummary
            );

            if (alert is not null)
            {
                context.MonitorAlerts.Add(alert);
            }

            context.ActivityLogEntries.Add(
                ActivityLogEntry.Create(
                    "monitor.executed",
                    monitor.OwnerUserName,
                    $"«{monitor.Name}»: {dashboard.Summary.Total} opiniones · {dashboard.Summary.Negative} negativas"
                )
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            monitor.MarkRun(now, 0, 0, 0, 0, $"Error: {exception.Message}");
            context.ActivityLogEntries.Add(
                ActivityLogEntry.Create(
                    "monitor.failed",
                    monitor.OwnerUserName,
                    $"«{monitor.Name}»: {exception.Message}"
                )
            );
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
