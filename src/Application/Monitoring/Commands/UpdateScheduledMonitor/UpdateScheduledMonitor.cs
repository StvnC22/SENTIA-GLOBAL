using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.Common.Security;
using AnalisisSentimiento.Application.Monitoring.Queries.GetScheduledMonitors;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.Monitoring.Commands.UpdateScheduledMonitor;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Analyst}")]
public record UpdateScheduledMonitorCommand(
    int Id,
    string Name,
    string Topic,
    int IntervalMinutes,
    bool IsActive,
    string? Platform = null,
    string? Province = null,
    string? Canton = null,
    string? Parish = null
) : ICommand<ScheduledMonitorDto>;

public class UpdateScheduledMonitorCommandValidator
    : AbstractValidator<UpdateScheduledMonitorCommand>
{
    public UpdateScheduledMonitorCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Topic).NotEmpty().MaximumLength(500);
        RuleFor(x => x.IntervalMinutes).InclusiveBetween(5, 24 * 60);
    }
}

internal class UpdateScheduledMonitorCommandHandler(IApplicationDbContext context, IUser user)
    : IRequestHandler<UpdateScheduledMonitorCommand, Result<ScheduledMonitorDto>>
{
    public async Task<Result<ScheduledMonitorDto>> Handle(
        UpdateScheduledMonitorCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = await context.ScheduledMonitors.FindAsync([request.Id], cancellationToken);
        if (entity is null)
        {
            return Result.Fail(new NotFoundError($"Monitoreo {request.Id} no encontrado."));
        }

        entity.Update(
            request.Name,
            request.Topic,
            request.IntervalMinutes,
            request.Platform,
            request.Province,
            request.Canton,
            request.Parish,
            request.IsActive
        );

        context.ActivityLogEntries.Add(
            ActivityLogEntry.Create(
                "monitor.updated",
                user.Id,
                $"Monitoreo «{entity.Name}» {(entity.IsActive ? "activo" : "pausado")}"
            )
        );
        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok(
            new ScheduledMonitorDto(
                entity.Id,
                entity.Name,
                entity.Topic,
                entity.Platform,
                entity.Province,
                entity.Canton,
                entity.Parish,
                entity.IntervalMinutes,
                entity.IsActive,
                entity.LastRunAt,
                entity.NextRunAt,
                entity.LastNegativeRatio,
                entity.LastTotal,
                entity.LastPositive,
                entity.LastNeutral,
                entity.LastNegative,
                entity.LastExecutiveSummary,
                entity.OwnerUserName
            )
        );
    }
}
