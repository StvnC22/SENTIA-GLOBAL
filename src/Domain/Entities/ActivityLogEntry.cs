namespace AnalisisSentimiento.Domain.Entities;

public class ActivityLogEntry : BaseEntity
{
    private ActivityLogEntry() { }

    public DateTimeOffset OccurredAt { get; private set; }
    public string? UserName { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Detail { get; private set; }

    public static ActivityLogEntry Create(string action, string? userName = null, string? detail = null)
    {
        return new ActivityLogEntry
        {
            OccurredAt = DateTimeOffset.UtcNow,
            UserName = string.IsNullOrWhiteSpace(userName) ? null : userName.Trim(),
            Action = action.Trim(),
            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
        };
    }
}
