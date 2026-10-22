namespace AnalisisSentimiento.Domain.Entities;

public class ScheduledMonitor : BaseAuditableEntity
{
    private ScheduledMonitor() { }

    public string Name { get; private set; } = string.Empty;
    public string Topic { get; private set; } = string.Empty;
    public string? Platform { get; private set; }
    public string? Province { get; private set; }
    public string? Canton { get; private set; }
    public string? Parish { get; private set; }
    public int IntervalMinutes { get; private set; } = 60;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? LastRunAt { get; private set; }
    public DateTimeOffset? NextRunAt { get; private set; }
    public double? LastNegativeRatio { get; private set; }
    public int? LastTotal { get; private set; }
    public int? LastPositive { get; private set; }
    public int? LastNeutral { get; private set; }
    public int? LastNegative { get; private set; }
    public string? LastExecutiveSummary { get; private set; }
    public string? OwnerUserName { get; private set; }

    public static ScheduledMonitor Create(
        string name,
        string topic,
        int intervalMinutes,
        string? platform = null,
        string? province = null,
        string? canton = null,
        string? parish = null,
        string? ownerUserName = null
    )
    {
        var normalizedInterval = Math.Clamp(intervalMinutes, 5, 24 * 60);
        var now = DateTimeOffset.UtcNow;

        return new ScheduledMonitor
        {
            Name = name.Trim(),
            Topic = topic.Trim(),
            Platform = NormalizeOptional(platform),
            Province = NormalizeOptional(province),
            Canton = NormalizeOptional(canton),
            Parish = NormalizeOptional(parish),
            IntervalMinutes = normalizedInterval,
            IsActive = true,
            NextRunAt = now,
            OwnerUserName = NormalizeOptional(ownerUserName),
        };
    }

    public void Update(
        string name,
        string topic,
        int intervalMinutes,
        string? platform,
        string? province,
        string? canton,
        string? parish,
        bool isActive
    )
    {
        Name = name.Trim();
        Topic = topic.Trim();
        Platform = NormalizeOptional(platform);
        Province = NormalizeOptional(province);
        Canton = NormalizeOptional(canton);
        Parish = NormalizeOptional(parish);
        IntervalMinutes = Math.Clamp(intervalMinutes, 5, 24 * 60);
        IsActive = isActive;
        if (isActive && NextRunAt is null)
        {
            NextRunAt = DateTimeOffset.UtcNow;
        }
    }

    public void MarkRun(
        DateTimeOffset runAt,
        int total,
        int positive,
        int neutral,
        int negative,
        string? executiveSummary
    )
    {
        LastRunAt = runAt;
        LastTotal = total;
        LastPositive = positive;
        LastNeutral = neutral;
        LastNegative = negative;
        LastNegativeRatio = total == 0 ? 0 : (double)negative / total;
        LastExecutiveSummary = executiveSummary;
        NextRunAt = runAt.AddMinutes(IntervalMinutes);
    }

    public const double SignificantChangeThreshold = 0.15;
    public const double AlertNegativeRatioFloor = 0.35;

    public MonitorAlert? RecordRun(
        DateTimeOffset runAt,
        int total,
        int positive,
        int neutral,
        int negative,
        string? executiveSummary
    )
    {
        var previousRatio = LastNegativeRatio;
        MarkRun(runAt, total, positive, neutral, negative, executiveSummary);
        var currentRatio = LastNegativeRatio ?? 0;

        if (previousRatio is null || total == 0)
        {
            return null;
        }

        var delta = currentRatio - previousRatio.Value;
        if (
            Math.Abs(delta) < SignificantChangeThreshold
            || Math.Max(currentRatio, previousRatio.Value) < AlertNegativeRatioFloor
        )
        {
            return null;
        }

        var direction = delta > 0 ? "subió" : "bajó";
        var message =
            $"El sentimiento negativo de «{Name}» {direction} a {currentRatio:P0} "
            + $"(antes {previousRatio.Value:P0}) para el tema «{Topic}».";
        return MonitorAlert.Create(Id, Name, message, previousRatio.Value, currentRatio);
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
        NextRunAt ??= DateTimeOffset.UtcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
