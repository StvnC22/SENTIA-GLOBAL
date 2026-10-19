using System.Text.Json;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;
using AnalisisSentimiento.Infrastructure.Configuration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.SocialListening;

public sealed class FileSocialMonitorStore : ISocialMonitorStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string filePath;

    public FileSocialMonitorStore(IOptions<SocialMonitorStoreOptions> options)
    {
        filePath =
            options.Value.FilePath
            ?? Path.Combine(LocalAppStorage.GetAppDataDirectory(), "social-monitors.json");
    }

    public async Task<IReadOnlyList<SocialMonitorSubscription>> GetAllAsync(
        CancellationToken cancellationToken
    )
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
                return [];
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<List<SocialMonitorSubscription>>(
                    stream,
                    cancellationToken: cancellationToken
                ) ?? [];
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<SocialMonitorSubscription>> GetForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken
    ) => (await GetAllAsync(cancellationToken)).Where(item => item.OwnerId == ownerId).ToArray();

    public async Task SaveAsync(
        SocialMonitorSubscription subscription,
        CancellationToken cancellationToken
    )
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            List<SocialMonitorSubscription> subscriptions = [];
            if (File.Exists(filePath))
            {
                await using var source = File.OpenRead(filePath);
                subscriptions =
                    await JsonSerializer.DeserializeAsync<List<SocialMonitorSubscription>>(
                        source,
                        cancellationToken: cancellationToken
                    ) ?? [];
            }

            subscriptions.RemoveAll(item => item.Id == subscription.Id);
            subscriptions.Add(subscription);
            var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
            await using (var destination = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    destination,
                    subscriptions,
                    cancellationToken: cancellationToken
                );
            }
            File.Move(temporaryPath, filePath, true);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task DeleteAsync(string id, string ownerId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
                return;
            await using var source = File.OpenRead(filePath);
            var subscriptions =
                await JsonSerializer.DeserializeAsync<List<SocialMonitorSubscription>>(
                    source,
                    cancellationToken: cancellationToken
                ) ?? [];
            if (subscriptions.RemoveAll(item => item.Id == id && item.OwnerId == ownerId) == 0)
                return;
            var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
            await using (var destination = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    destination,
                    subscriptions,
                    cancellationToken: cancellationToken
                );
            }
            File.Move(temporaryPath, filePath, true);
        }
        finally
        {
            gate.Release();
        }
    }
}

public sealed class SocialMonitorStoreOptions
{
    public string? FilePath { get; set; }
}

public sealed class ScheduledSocialMonitorService(
    IServiceScopeFactory scopeFactory,
    ISocialMonitorStore monitorStore,
    TimeProvider timeProvider,
    ILogger<ScheduledSocialMonitorService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunDueMonitorsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }

    private async Task RunDueMonitorsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var due = (await monitorStore.GetAllAsync(cancellationToken))
            .Where(subscription => subscription.Enabled && subscription.NextRunAt <= now)
            .ToArray();

        foreach (var subscription in due)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var dashboard = await sender.Send(
                    new GetSocialCommentsQuery(
                        Platform: subscription.Platform,
                        Refresh: true,
                        Topic: subscription.Topic,
                        SourceUrls: subscription.SourceUrls,
                        Search: subscription.Search,
                        From: subscription.From,
                        To: subscription.To,
                        Province: subscription.Province,
                        Canton: subscription.Canton,
                        Parish: subscription.Parish
                    ),
                    cancellationToken
                );
                var rate =
                    dashboard.Summary.Total == 0
                        ? subscription.PreviousNegativeRate
                        : (int)
                            Math.Round(
                                (double)dashboard.Summary.Negative / dashboard.Summary.Total * 100
                            );
                var alert = subscription.LastAlert;
                if (
                    rate is not null
                    && subscription.PreviousNegativeRate is not null
                    && Math.Abs(rate.Value - subscription.PreviousNegativeRate.Value)
                        >= subscription.AlertThreshold
                )
                {
                    var direction = rate > subscription.PreviousNegativeRate ? "Subió" : "Bajó";
                    alert =
                        $"{direction} el sentimiento negativo de {subscription.PreviousNegativeRate}% a {rate}%";
                }
                await monitorStore.SaveAsync(
                    subscription with
                    {
                        NextRunAt = now.AddMinutes(subscription.IntervalMinutes),
                        PreviousNegativeRate = rate,
                        LastAlert = alert,
                        LastRunAt = now,
                        LastError = dashboard.Message,
                    },
                    cancellationToken
                );
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(
                    exception,
                    "Falló el monitoreo programado {MonitorId}",
                    subscription.Id
                );
                await monitorStore.SaveAsync(
                    subscription with
                    {
                        NextRunAt = now.AddMinutes(subscription.IntervalMinutes),
                        LastRunAt = now,
                        LastError = exception.Message,
                    },
                    cancellationToken
                );
            }
        }
    }
}
