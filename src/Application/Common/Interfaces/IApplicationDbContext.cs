using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TodoList> TodoLists { get; }

    DbSet<TodoItem> TodoItems { get; }

    DbSet<ScheduledMonitor> ScheduledMonitors { get; }

    DbSet<MonitorAlert> MonitorAlerts { get; }

    DbSet<ActivityLogEntry> ActivityLogEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
