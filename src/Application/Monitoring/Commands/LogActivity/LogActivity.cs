using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.Monitoring.Commands.LogActivity;

public record LogActivityCommand(string Action, string? Detail = null) : ICommand;

public class LogActivityCommandValidator : AbstractValidator<LogActivityCommand>
{
    public LogActivityCommandValidator()
    {
        RuleFor(x => x.Action).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Detail).MaximumLength(2000);
    }
}

internal class LogActivityCommandHandler(IApplicationDbContext context, IUser user)
    : IRequestHandler<LogActivityCommand, Result>
{
    public async Task<Result> Handle(
        LogActivityCommand request,
        CancellationToken cancellationToken
    )
    {
        context.ActivityLogEntries.Add(
            ActivityLogEntry.Create(request.Action, user.Id, request.Detail)
        );
        await context.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
