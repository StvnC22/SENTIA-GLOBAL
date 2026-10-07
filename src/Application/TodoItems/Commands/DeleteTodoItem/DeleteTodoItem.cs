using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.TodoItems.Commands.DeleteTodoItem;

public record DeleteTodoItemCommand(int Id) : ICommand;

internal class DeleteTodoItemCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteTodoItemCommand, Result>
{
    public async Task<Result> Handle(
        DeleteTodoItemCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = await context.TodoItems.FindAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fail(new NotFoundError($"TodoItem {request.Id} was not found."));
        }

        context.TodoItems.Remove(entity);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
