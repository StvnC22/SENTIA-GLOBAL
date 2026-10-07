using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Domain.Entities;
using AnalisisSentimiento.Domain.ValueObjects;

namespace AnalisisSentimiento.Application.TodoLists.Commands.CreateTodoList;

public record CreateTodoListCommand : ICommand<int>
{
    public string? Title { get; init; }

    public string? Colour { get; init; }
}

internal class CreateTodoListCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateTodoListCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        CreateTodoListCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = TodoList.Create(request.Title, Colour.From(request.Colour ?? Colour.Grey));

        context.TodoLists.Add(entity);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok(entity.Id);
    }
}

public class CreateTodoListCommandValidator : AbstractValidator<CreateTodoListCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateTodoListCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.Title)
            .NotEmpty()
            .MaximumLength(200)
            .MustAsync(BeUniqueTitle)
            .WithMessage("'{PropertyName}' must be unique.")
            .WithErrorCode("Unique");
    }

    public async Task<bool> BeUniqueTitle(string title, CancellationToken cancellationToken)
    {
        return !await _context.TodoLists.AnyAsync(l => l.Title == title, cancellationToken);
    }
}
