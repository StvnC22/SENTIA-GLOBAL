using AnalisisSentimiento.Application.Monitoring.Commands.ExecuteDueMonitors;
using MediatR;
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
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(new ExecuteDueMonitorsCommand(), stoppingToken);
                if (result.IsSuccess && result.Value > 0)
                {
                    logger.LogInformation(
                        "Se ejecutaron {Count} monitoreos periódicos.",
                        result.Value
                    );
                }
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
}
