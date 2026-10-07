using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Domain.Enums;

namespace AnalisisSentimiento.Application.TodoItems.Commands.UpdateTodoItemDetail;

public record UpdateTodoItemDetailCommand : ICommand
{
    public int Id { get; init; }

    public int ListId { get; init; }

    public PriorityLevel Priority { get; init; } = PriorityLevel.None;

    public string? Note { get; init; }
}

internal class UpdateTodoItemDetailCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateTodoItemDetailCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTodoItemDetailCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = await context.TodoItems.FindAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fail(new NotFoundError($"TodoItem {request.Id} was not found."));
        }

        entity.UpdateDetail(request.ListId, request.Priority, request.Note);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
