using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.Common.Security;
using AnalisisSentimiento.Application.Monitoring.Queries.GetScheduledMonitors;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.Monitoring.Commands.CreateScheduledMonitor;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Analyst}")]
public record CreateScheduledMonitorCommand(
    string Name,
    string Topic,
    int IntervalMinutes,
    string? Platform = null,
    string? Province = null,
    string? Canton = null,
    string? Parish = null
) : ICommand<ScheduledMonitorDto>;

public class CreateScheduledMonitorCommandValidator : AbstractValidator<CreateScheduledMonitorCommand>
{
    public CreateScheduledMonitorCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Topic).NotEmpty().MaximumLength(500);
        RuleFor(x => x.IntervalMinutes).InclusiveBetween(5, 24 * 60);
    }
}

internal class CreateScheduledMonitorCommandHandler(IApplicationDbContext context, IUser user)
    : IRequestHandler<CreateScheduledMonitorCommand, Result<ScheduledMonitorDto>>
{
    public async Task<Result<ScheduledMonitorDto>> Handle(
        CreateScheduledMonitorCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = ScheduledMonitor.Create(
            request.Name,
            request.Topic,
            request.IntervalMinutes,
            request.Platform,
            request.Province,
            request.Canton,
            request.Parish,
            user.Id
        );

        context.ScheduledMonitors.Add(entity);
        context.ActivityLogEntries.Add(
            ActivityLogEntry.Create(
                "monitor.created",
                user.Id,
                $"Monitoreo «{entity.Name}» cada {entity.IntervalMinutes} min"
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
