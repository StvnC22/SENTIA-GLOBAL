using AnalisisSentimiento.Application.TodoItems.Commands.CreateTodoItem;
using AnalisisSentimiento.Application.TodoItems.Commands.DeleteTodoItem;
using AnalisisSentimiento.Application.TodoItems.Commands.UpdateTodoItem;
using AnalisisSentimiento.Application.TodoItems.Commands.UpdateTodoItemDetail;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AnalisisSentimiento.Web.Endpoints;

public class TodoItems : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapPost(CreateTodoItem);
        groupBuilder.MapPut(UpdateTodoItem, "{id}");
        groupBuilder.MapPatch(UpdateTodoItemDetail, "UpdateDetail/{id}");
        groupBuilder.MapDelete(DeleteTodoItem, "{id}");
    }

    [EndpointSummary("Create a new Todo Item")]
    [EndpointDescription(
        "Creates a new todo item using the provided details and returns the ID of the created item."
    )]
    public static async Task<Results<Created<int>, ProblemHttpResult>> CreateTodoItem(
        ISender sender,
        CreateTodoItemCommand command
    )
    {
        var result = await sender.Send(command);

        return result.IsSuccess
            ? TypedResults.Created($"/{nameof(TodoItems)}/{result.Value}", result.Value)
            : result.ToProblemHttpResult();
    }

    [EndpointSummary("Update a Todo Item")]
    [EndpointDescription(
        "Updates the specified todo item. The ID in the URL must match the ID in the payload."
    )]
    public static async Task<Results<NoContent, BadRequest, ProblemHttpResult>> UpdateTodoItem(
        ISender sender,
        int id,
        UpdateTodoItemCommand command
    )
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        var result = await sender.Send(command);

        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemHttpResult();
    }

    [EndpointSummary("Update Todo Item Details")]
    [EndpointDescription(
        "Updates the detail fields of a specific todo item. The ID in the URL must match the ID in the payload."
    )]
    public static async Task<
        Results<NoContent, BadRequest, ProblemHttpResult>
    > UpdateTodoItemDetail(ISender sender, int id, UpdateTodoItemDetailCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        var result = await sender.Send(command);

        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemHttpResult();
    }

    [EndpointSummary("Delete a Todo Item")]
    [EndpointDescription("Deletes the todo item with the specified ID.")]
    public static async Task<Results<NoContent, ProblemHttpResult>> DeleteTodoItem(
        ISender sender,
        int id
    )
    {
        var result = await sender.Send(new DeleteTodoItemCommand(id));

        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemHttpResult();
    }
}
