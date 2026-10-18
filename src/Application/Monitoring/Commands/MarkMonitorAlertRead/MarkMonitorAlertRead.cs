using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.Common.Security;
using AnalisisSentimiento.Domain.Constants;

namespace AnalisisSentimiento.Application.Monitoring.Commands.MarkMonitorAlertRead;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Analyst},{Roles.Consulta}")]
public record MarkMonitorAlertReadCommand(int Id) : ICommand;

internal class MarkMonitorAlertReadCommandHandler(IApplicationDbContext context)
    : IRequestHandler<MarkMonitorAlertReadCommand, Result>
{
    public async Task<Result> Handle(
        MarkMonitorAlertReadCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = await context.MonitorAlerts.FindAsync([request.Id], cancellationToken);
        if (entity is null)
        {
            return Result.Fail(new NotFoundError($"Alerta {request.Id} no encontrada."));
        }

        entity.MarkRead();
        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
