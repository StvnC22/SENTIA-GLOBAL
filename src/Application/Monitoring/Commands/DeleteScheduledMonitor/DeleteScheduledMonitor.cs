using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.Common.Security;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.Monitoring.Commands.DeleteScheduledMonitor;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Analyst}")]
public record DeleteScheduledMonitorCommand(int Id) : ICommand;

internal class DeleteScheduledMonitorCommandHandler(IApplicationDbContext context, IUser user)
    : IRequestHandler<DeleteScheduledMonitorCommand, Result>
{
    public async Task<Result> Handle(
        DeleteScheduledMonitorCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = await context.ScheduledMonitors.FindAsync([request.Id], cancellationToken);
        if (entity is null)
        {
            return Result.Fail(new NotFoundError($"Monitoreo {request.Id} no encontrado."));
        }

        var name = entity.Name;
        context.ScheduledMonitors.Remove(entity);
        context.ActivityLogEntries.Add(
            ActivityLogEntry.Create("monitor.deleted", user.Id, $"Monitoreo «{name}» eliminado")
        );
        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
