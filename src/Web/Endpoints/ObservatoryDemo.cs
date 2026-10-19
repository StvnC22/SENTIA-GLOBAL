using AnalisisSentimiento.Application.ObservatoryDemo;
using AnalisisSentimiento.Application.ObservatoryDemo.Queries.GetObservatoryDemo;
using AnalisisSentimiento.Application.ObservatoryDemo.Queries.GetObservatoryDemoExport;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AnalisisSentimiento.Web.Endpoints;

public class ObservatoryDemo : IEndpointGroup
{
    public static string RoutePrefix => "/api/observatory-demo";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetDashboard);
        groupBuilder.MapGet(Export, "export");
    }

    [EndpointSummary("Demo del observatorio territorial")]
    [EndpointDescription(
        "Devuelve indicadores de demostración (territorio, alertas, roles, auditoría). No altera el monitoreo social real."
    )]
    public static async Task<Ok<ObservatoryDemoDashboard>> GetDashboard(ISender sender)
    {
        var dashboard = await sender.Send(new GetObservatoryDemoQuery());
        return TypedResults.Ok(dashboard);
    }

    [EndpointSummary("Exportar CSV de la demo territorial")]
    public static async Task<FileContentHttpResult> Export(ISender sender, string format = "csv")
    {
        var file = await sender.Send(new GetObservatoryDemoExportQuery(format));
        return TypedResults.File(file.Content, file.ContentType, file.FileName);
    }
}
