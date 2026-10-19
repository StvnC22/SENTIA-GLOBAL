using System.Security.Claims;
using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.SocialListening.Commands.DeleteSocialMonitorSubscription;
using AnalisisSentimiento.Application.SocialListening.Commands.SaveSocialMonitorSubscription;
using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialMonitorSubscriptions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AnalisisSentimiento.Web.Endpoints;

public class SocialMonitoring : IEndpointGroup
{
    public static string RoutePrefix => "/api/social-monitoring";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(Get);
        groupBuilder.MapPut(Save, string.Empty);
        groupBuilder.MapDelete(Delete, "{id}");
    }

    public static async Task<Ok<IReadOnlyList<SocialMonitorSubscription>>> Get(
        HttpContext context,
        ISender sender,
        CancellationToken cancellationToken
    ) =>
        TypedResults.Ok(
            await sender.Send(
                new GetSocialMonitorSubscriptionsQuery(GetOwnerId(context)),
                cancellationToken
            )
        );

    public static async Task<Results<Ok<SocialMonitorSubscription>, BadRequest<string>>> Save(
        HttpContext context,
        ISender sender,
        TimeProvider timeProvider,
        SaveSocialMonitorRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request.IntervalMinutes is < 15 or > 1440)
            return TypedResults.BadRequest("La frecuencia debe estar entre 15 y 1440 minutos.");
        if (request.AlertThreshold is < 1 or > 100)
            return TypedResults.BadRequest(
                "El umbral debe estar entre 1 y 100 puntos porcentuales."
            );
        if (request.From is not null && request.To is not null && request.From > request.To)
            return TypedResults.BadRequest("La fecha inicial no puede ser posterior a la fecha final.");

        var ownerId = GetOwnerId(context);
        var subscriptions = await sender.Send(
            new GetSocialMonitorSubscriptionsQuery(ownerId),
            cancellationToken
        );
        var existing = request.Id is null
            ? null
            : subscriptions.FirstOrDefault(item => item.Id == request.Id);
        if (request.Id is not null && existing is null)
            return TypedResults.BadRequest("El monitoreo no existe para este usuario.");

        var now = timeProvider.GetUtcNow();
        var subscription = new SocialMonitorSubscription(
            existing?.Id ?? Guid.NewGuid().ToString("N"),
            ownerId,
            request.Enabled,
            request.IntervalMinutes,
            request.AlertThreshold,
            request.Platform,
            request.Topic?.Trim(),
            request.Search?.Trim(),
            request.SourceUrls ?? new Dictionary<string, string>(),
            now.AddMinutes(request.IntervalMinutes),
            existing?.PreviousNegativeRate,
            existing?.LastAlert,
            existing?.LastRunAt,
            existing?.LastError,
            request.From,
            request.To,
            request.Province?.Trim(),
            request.Canton?.Trim(),
            request.Parish?.Trim()
        );
        var result = await sender.Send(
            new SaveSocialMonitorSubscriptionCommand(subscription),
            cancellationToken
        );
        return result.IsSuccess
            ? TypedResults.Ok(result.Value!)
            : TypedResults.BadRequest(
                string.Join("; ", result.Errors.Select(error => error.Message))
            );
    }

    public static async Task<Results<NoContent, NotFound>> Delete(
        string id,
        HttpContext context,
        ISender sender,
        CancellationToken cancellationToken
    )
    {
        var ownerId = GetOwnerId(context);
        var subscriptions = await sender.Send(
            new GetSocialMonitorSubscriptionsQuery(ownerId),
            cancellationToken
        );
        var exists = subscriptions.Any(item => item.Id == id);
        if (!exists)
            return TypedResults.NotFound();
        await sender.Send(
            new DeleteSocialMonitorSubscriptionCommand(id, ownerId),
            cancellationToken
        );
        return TypedResults.NoContent();
    }

    private static string GetOwnerId(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "local";
}

public sealed record SaveSocialMonitorRequest(
    string? Id,
    bool Enabled,
    int IntervalMinutes,
    int AlertThreshold,
    string? Platform,
    string? Topic,
    string? Search,
    IReadOnlyDictionary<string, string>? SourceUrls,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Province = null,
    string? Canton = null,
    string? Parish = null
);
