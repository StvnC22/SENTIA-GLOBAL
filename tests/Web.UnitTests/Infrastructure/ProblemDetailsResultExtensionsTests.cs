using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Web.Infrastructure;
using FluentResults;
using NUnit.Framework;
using Shouldly;

namespace AnalisisSentimiento.Web.UnitTests.Infrastructure;

public class ProblemDetailsResultExtensionsTests
{
    [Test]
    public void NotFoundErrorShouldMapTo404()
    {
        var result = Result.Fail(new NotFoundError("TodoItem 99 was not found."));

        var problem = result.ToProblemHttpResult();

        problem.StatusCode.ShouldBe(404);
        problem.ProblemDetails.Detail.ShouldBe("TodoItem 99 was not found.");
    }

    [Test]
    public void ConflictErrorShouldMapTo409()
    {
        var result = Result.Fail(new ConflictError("Title must be unique."));

        var problem = result.ToProblemHttpResult();

        problem.StatusCode.ShouldBe(409);
    }

    [Test]
    public void UntypedErrorShouldMapTo400()
    {
        var result = Result.Fail("Something went wrong.");

        var problem = result.ToProblemHttpResult();

        problem.StatusCode.ShouldBe(400);
    }

    [Test]
    public void ShouldJoinMultipleErrorMessagesInDetail()
    {
        var result = Result.Fail(new NotFoundError("List not found.")).WithError("Item not found.");

        var problem = result.ToProblemHttpResult();

        problem.ProblemDetails.Detail.ShouldBe("List not found. Item not found.");
    }
}
