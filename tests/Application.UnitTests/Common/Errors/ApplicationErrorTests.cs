using AnalisisSentimiento.Application.Common.Errors;
using NUnit.Framework;
using Shouldly;

namespace AnalisisSentimiento.Application.UnitTests.Common.Errors;

public class ApplicationErrorTests
{
    [Test]
    public void NotFoundErrorShouldCarryNotFoundType()
    {
        var error = new NotFoundError("TodoItem 99 was not found.");

        error.Type.ShouldBe(ApplicationErrorType.NotFound);
        error.Message.ShouldBe("TodoItem 99 was not found.");
    }

    [Test]
    public void ConflictErrorShouldCarryConflictType()
    {
        var error = new ConflictError("Title must be unique.");

        error.Type.ShouldBe(ApplicationErrorType.Conflict);
        error.Message.ShouldBe("Title must be unique.");
    }
}
