using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.TodoLists.Commands.DeleteTodoList;

public record DeleteTodoListCommand(int Id) : ICommand;

internal class DeleteTodoListCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteTodoListCommand, Result>
{
    public async Task<Result> Handle(
        DeleteTodoListCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = await context
            .TodoLists.Where(l => l.Id == request.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return Result.Fail(new NotFoundError($"TodoList {request.Id} was not found."));
        }

        context.TodoLists.Remove(entity);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
