using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;
using AnalisisSentimiento.Domain.Entities;
using AnalisisSentimiento.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AnalisisSentimiento.Infrastructure.Monitoring;

public sealed class ScheduledMonitorWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledMonitorWorker> logger
) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(45);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker de monitoreos periódicos iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueMonitorsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error en el ciclo de monitoreos periódicos.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunDueMonitorsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();

        var dueMonitors = await context
            .ScheduledMonitors.Where(x =>
                x.IsActive && (x.NextRunAt == null || x.NextRunAt <= now)
            )
            .OrderBy(x => x.NextRunAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var monitor in dueMonitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteMonitorAsync(context, sender, monitor, now, cancellationToken);
        }
    }

    private async Task ExecuteMonitorAsync(
        ApplicationDbContext context,
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

            var previousRatio = monitor.LastNegativeRatio;
            monitor.MarkRun(
                now,
                dashboard.Summary.Total,
                dashboard.Summary.Positive,
                dashboard.Summary.Neutral,
                dashboard.Summary.Negative,
                dashboard.Insights?.ExecutiveSummary
            );

            var currentRatio = monitor.LastNegativeRatio ?? 0;
            if (
                previousRatio is not null
                && currentRatio - previousRatio.Value >= 0.15
                && currentRatio >= 0.35
            )
            {
                var message =
                    $"El sentimiento negativo de «{monitor.Name}» subió a {currentRatio:P0} "
                    + $"(antes {previousRatio.Value:P0}) para el tema «{monitor.Topic}».";
                context.MonitorAlerts.Add(
                    MonitorAlert.Create(
                        monitor.Id,
                        monitor.Name,
                        message,
                        previousRatio.Value,
                        currentRatio
                    )
                );
            }

            context.ActivityLogEntries.Add(
                ActivityLogEntry.Create(
                    "monitor.executed",
                    monitor.OwnerUserName,
                    $"«{monitor.Name}»: {dashboard.Summary.Total} opiniones · neg {dashboard.Summary.Negative}"
                )
            );

            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Monitoreo {MonitorId} «{Name}» ejecutado ({Total} opiniones).",
                monitor.Id,
                monitor.Name,
                dashboard.Summary.Total
            );
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Falló la ejecución del monitoreo {MonitorId} «{Name}».",
                monitor.Id,
                monitor.Name
            );
            monitor.MarkRun(now, 0, 0, 0, 0, $"Error: {exception.Message}");
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
