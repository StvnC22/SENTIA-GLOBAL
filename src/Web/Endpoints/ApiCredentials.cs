using AnalisisSentimiento.Application.ApiCredentials.Commands.SaveApiCredentials;
using AnalisisSentimiento.Application.ApiCredentials.Queries.GetApiCredentialStatus;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AnalisisSentimiento.Web.Endpoints;

public class ApiCredentials : IEndpointGroup
{
    public static string RoutePrefix => "/api/api-credentials";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetStatus);
        groupBuilder.MapPut(Save, string.Empty);
    }

    [EndpointSummary("Consultar estado de las claves API")]
    [EndpointDescription("Solo devuelve si cada clave está configurada; nunca expone sus valores.")]
    public static async Task<Ok<ApiCredentialStatus>> GetStatus(ISender sender)
    {
        var status = await sender.Send(new GetApiCredentialStatusQuery());
        return TypedResults.Ok(status);
    }

    [EndpointSummary("Guardar claves API")]
    [EndpointDescription("Cifra y guarda localmente las claves proporcionadas.")]
    public static async Task<Results<Ok<ApiCredentialStatus>, ProblemHttpResult>> Save(
        ISender sender,
        SaveApiCredentialsCommand command
    )
    {
        var result = await sender.Send(command);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemHttpResult();
    }
}
