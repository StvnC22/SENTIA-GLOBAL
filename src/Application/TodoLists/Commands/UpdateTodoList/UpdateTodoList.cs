using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Domain.ValueObjects;

namespace AnalisisSentimiento.Application.TodoLists.Commands.UpdateTodoList;

public record UpdateTodoListCommand : ICommand
{
    public int Id { get; init; }

    public string? Title { get; init; }

    public string? Colour { get; init; }
}

internal class UpdateTodoListCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateTodoListCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTodoListCommand request,
        CancellationToken cancellationToken
    )
    {
        var entity = await context.TodoLists.FindAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fail(new NotFoundError($"TodoList {request.Id} was not found."));
        }

        entity.Rename(request.Title);

        if (request.Colour is not null)
        {
            entity.UpdateColour(Colour.From(request.Colour));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

public class UpdateTodoListCommandValidator : AbstractValidator<UpdateTodoListCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateTodoListCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.Title)
            .NotEmpty()
            .MaximumLength(200)
            .MustAsync(BeUniqueTitle)
            .WithMessage("'{PropertyName}' must be unique.")
            .WithErrorCode("Unique");
    }

    public async Task<bool> BeUniqueTitle(
        UpdateTodoListCommand model,
        string title,
        CancellationToken cancellationToken
    )
    {
        return !await _context
            .TodoLists.Where(l => l.Id != model.Id)
            .AnyAsync(l => l.Title == title, cancellationToken);
    }
}
