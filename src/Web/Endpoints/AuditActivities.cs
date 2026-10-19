using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.SocialListening.Queries.GetRecentAuditActivities;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AnalisisSentimiento.Web.Endpoints;

public class AuditActivities : IEndpointGroup
{
    public static string RoutePrefix => "/api/audit-activities";

    public static void Map(RouteGroupBuilder groupBuilder) =>
        groupBuilder.MapGet(GetRecent).RequireAuthorization("ManageUsers");

    [EndpointSummary("Consultar actividad reciente del sistema")]
    public static async Task<Ok<IReadOnlyList<AuditActivity>>> GetRecent(
        ISender sender,
        int count = 100,
        CancellationToken cancellationToken = default
    ) =>
        TypedResults.Ok(
            await sender.Send(new GetRecentAuditActivitiesQuery(count), cancellationToken)
        );
}
