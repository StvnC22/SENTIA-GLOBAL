using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.TodoItems.Commands.UpdateTodoItem;

public record UpdateTodoItemCommand : ICommand
{
    public int Id { get; init; }

    public string? Title { get; init; }

    public bool Done { get; init; }
}

internal class UpdateTodoItemCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateTodoItemCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTodoItemCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = await context.TodoItems.FindAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fail(new NotFoundError($"TodoItem {request.Id} was not found."));
        }

        entity.Update(request.Title, request.Done);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

public class UpdateTodoItemCommandValidator : AbstractValidator<UpdateTodoItemCommand>
{
    public UpdateTodoItemCommandValidator()
    {
        RuleFor(v => v.Title).MaximumLength(200).NotEmpty();
    }
}
