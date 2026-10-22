using AnalisisSentimiento.Application.Monitoring.Commands.CreateScheduledMonitor;
using AnalisisSentimiento.Application.Monitoring.Commands.DeleteScheduledMonitor;
using AnalisisSentimiento.Application.Monitoring.Commands.MarkMonitorAlertRead;
using AnalisisSentimiento.Application.Monitoring.Commands.RunScheduledMonitorNow;
using AnalisisSentimiento.Application.Monitoring.Commands.UpdateScheduledMonitor;
using AnalisisSentimiento.Application.Monitoring.Queries.GetActivityLog;
using AnalisisSentimiento.Application.Monitoring.Queries.GetMonitorAlerts;
using AnalisisSentimiento.Application.Monitoring.Queries.GetScheduledMonitors;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AnalisisSentimiento.Web.Endpoints;

public class Monitors : IEndpointGroup
{
    public static string RoutePrefix => "/api/monitors";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet(GetMonitors);
        groupBuilder.MapPost(CreateMonitor);
        groupBuilder.MapPut(UpdateMonitor, "{id:int}");
        groupBuilder.MapDelete(DeleteMonitor, "{id:int}");
        groupBuilder.MapPost(RunMonitorNow, "{id:int}/run");
        groupBuilder.MapGet(GetAlerts, "alerts");
        groupBuilder.MapPost(MarkAlertRead, "alerts/{id:int}/read");
        groupBuilder.MapGet(GetActivity, "activity");
    }

    public static async Task<Ok<IReadOnlyList<ScheduledMonitorDto>>> GetMonitors(ISender sender)
    {
        var items = await sender.Send(new GetScheduledMonitorsQuery());
        return TypedResults.Ok(items);
    }

    public static async Task<
        Results<Created<ScheduledMonitorDto>, ProblemHttpResult>
    > CreateMonitor(ISender sender, CreateScheduledMonitorCommand command)
    {
        var result = await sender.Send(command);
        return result.IsSuccess
            ? TypedResults.Created($"/api/monitors/{result.Value.Id}", result.Value)
            : result.ToProblemHttpResult();
    }

    public static async Task<
        Results<Ok<ScheduledMonitorDto>, BadRequest, ProblemHttpResult>
    > UpdateMonitor(ISender sender, int id, UpdateScheduledMonitorCommand command)
    {
        if (id != command.Id)
        {
            return TypedResults.BadRequest();
        }

        var result = await sender.Send(command);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemHttpResult();
    }

    public static async Task<Results<NoContent, BadRequest, ProblemHttpResult>> DeleteMonitor(
        ISender sender,
        int id
    )
    {
        var result = await sender.Send(new DeleteScheduledMonitorCommand(id));
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemHttpResult();
    }

    public static async Task<Results<Ok<ScheduledMonitorDto>, ProblemHttpResult>> RunMonitorNow(
        ISender sender,
        int id
    )
    {
        var result = await sender.Send(new RunScheduledMonitorNowCommand(id));
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemHttpResult();
    }

    public static async Task<Ok<IReadOnlyList<MonitorAlertDto>>> GetAlerts(
        ISender sender,
        bool unreadOnly = false
    )
    {
        var items = await sender.Send(new GetMonitorAlertsQuery(unreadOnly));
        return TypedResults.Ok(items);
    }

    public static async Task<Results<NoContent, ProblemHttpResult>> MarkAlertRead(
        ISender sender,
        int id
    )
    {
        var result = await sender.Send(new MarkMonitorAlertReadCommand(id));
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemHttpResult();
    }

    public static async Task<Ok<IReadOnlyList<ActivityLogDto>>> GetActivity(
        ISender sender,
        int take = 40
    )
    {
        var items = await sender.Send(new GetActivityLogQuery(take));
        return TypedResults.Ok(items);
    }
}
