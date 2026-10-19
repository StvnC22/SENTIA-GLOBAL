using System.Text.Json;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.SocialListening;

public sealed class FileAuditActivityStore : IAuditActivityStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string filePath;

    public FileAuditActivityStore(IOptions<AuditActivityStoreOptions> options)
    {
        filePath =
            options.Value.FilePath
            ?? Path.Combine(LocalAppStorage.GetAppDataDirectory(), "audit-activity.jsonl");
    }

    public async Task AppendAsync(AuditActivity activity, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.AppendAllTextAsync(
                filePath,
                JsonSerializer.Serialize(activity) + Environment.NewLine,
                cancellationToken
            );
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            if (lines.Length > 5000)
            {
                await File.WriteAllLinesAsync(filePath, lines[^5000..], cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<AuditActivity>> GetRecentAsync(
        int count,
        CancellationToken cancellationToken
    )
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
                return [];
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            return lines
                .TakeLast(Math.Clamp(count, 1, 500))
                .Select(line => JsonSerializer.Deserialize<AuditActivity>(line))
                .Where(activity => activity is not null)
                .Select(activity => activity!)
                .Reverse()
                .ToArray();
        }
        finally
        {
            gate.Release();
        }
    }
}

public sealed class AuditActivityStoreOptions
{
    public string? FilePath { get; set; }
}
