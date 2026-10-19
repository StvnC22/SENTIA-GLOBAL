using System.Security.Claims;
using AnalisisSentimiento.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace AnalisisSentimiento.Web.Infrastructure;

public sealed class AuditActivityMiddleware(
    RequestDelegate next,
    ILogger<AuditActivityMiddleware> logger
)
{
    public async Task InvokeAsync(
        HttpContext context,
        IAuditActivityStore auditStore,
        TimeProvider timeProvider
    )
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);
        }
        finally
        {
            var activity = new AuditActivity(
                timeProvider.GetUtcNow(),
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "local",
                context.Request.Method,
                context.Request.Path.Value ?? "/api",
                context.Response.StatusCode
            );
            try
            {
                await auditStore.AppendAsync(activity, CancellationToken.None);
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogError(exception, "No se pudo guardar la actividad de auditoría");
            }
        }
    }
}
