namespace AnalisisSentimiento.Domain.Entities;

public class MonitorAlert : BaseAuditableEntity
{
    private MonitorAlert() { }

    public int ScheduledMonitorId { get; private set; }
    public string MonitorName { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public double PreviousNegativeRatio { get; private set; }
    public double CurrentNegativeRatio { get; private set; }
    public bool IsRead { get; private set; }

    public static MonitorAlert Create(
        int scheduledMonitorId,
        string monitorName,
        string message,
        double previousNegativeRatio,
        double currentNegativeRatio
    )
    {
        return new MonitorAlert
        {
            ScheduledMonitorId = scheduledMonitorId,
            MonitorName = monitorName,
            Message = message,
            PreviousNegativeRatio = previousNegativeRatio,
            CurrentNegativeRatio = currentNegativeRatio,
            IsRead = false,
        };
    }

    public void MarkRead() => IsRead = true;
}
