using Ardalis.SmartEnum;

namespace AnalisisSentimiento.Application.Common.Errors;

public sealed class ApplicationErrorType : SmartEnum<ApplicationErrorType>
{
    public static readonly ApplicationErrorType NotFound = new(nameof(NotFound), 1);
    public static readonly ApplicationErrorType Conflict = new(nameof(Conflict), 2);

    private ApplicationErrorType(string name, int value)
        : base(name, value) { }
}

public class ApplicationError(ApplicationErrorType type, string message) : Error(message)
{
    public ApplicationErrorType Type { get; } = type;
}

public class NotFoundError(string message)
    : ApplicationError(ApplicationErrorType.NotFound, message);

public class ConflictError(string message)
    : ApplicationError(ApplicationErrorType.Conflict, message);
