using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.TodoItems.Commands.CreateTodoItem;

public record CreateTodoItemCommand : ICommand<int>
{
    public int ListId { get; init; }

    public string? Title { get; init; }
}

internal class CreateTodoItemCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateTodoItemCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        CreateTodoItemCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = TodoItem.Create(request.ListId, request.Title);

        context.TodoItems.Add(entity);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok(entity.Id);
    }
}

public class CreateTodoItemCommandValidator : AbstractValidator<CreateTodoItemCommand>
{
    public CreateTodoItemCommandValidator()
    {
        RuleFor(v => v.Title).MaximumLength(200).NotEmpty();
    }
}
