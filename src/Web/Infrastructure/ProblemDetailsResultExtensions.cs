using AnalisisSentimiento.Application.Common.Errors;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AnalisisSentimiento.Web.Infrastructure;

public static class ProblemDetailsResultExtensions
{
    public static ProblemHttpResult ToProblemHttpResult(this ResultBase result)
    {
        var applicationError = result.Errors.OfType<ApplicationError>().FirstOrDefault();

        var statusCode = applicationError?.Type.Name switch
        {
            nameof(ApplicationErrorType.NotFound) => StatusCodes.Status404NotFound,
            nameof(ApplicationErrorType.Conflict) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return TypedResults.Problem(
            detail: string.Join(" ", result.Errors.Select(e => e.Message)),
            statusCode: statusCode
        );
    }
}
