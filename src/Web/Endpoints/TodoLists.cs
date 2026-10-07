using AnalisisSentimiento.Application.TodoLists.Commands.CreateTodoList;
using AnalisisSentimiento.Application.TodoLists.Commands.DeleteTodoList;
using AnalisisSentimiento.Application.TodoLists.Commands.UpdateTodoList;
using AnalisisSentimiento.Application.TodoLists.Queries.GetTodos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AnalisisSentimiento.Web.Endpoints;

public class TodoLists : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet(GetTodoLists);
        groupBuilder.MapPost(CreateTodoList);
        groupBuilder.MapPut(UpdateTodoList, "{id}");
        groupBuilder.MapDelete(DeleteTodoList, "{id}");
    }

    [EndpointSummary("Get all Todo Lists")]
    [EndpointDescription("Retrieves all todo lists along with their items.")]
    public static async Task<Ok<TodosVm>> GetTodoLists(ISender sender)
    {
        var vm = await sender.Send(new GetTodosQuery());

        return TypedResults.Ok(vm);
    }

    [EndpointSummary("Create a new Todo List")]
    [EndpointDescription(
        "Creates a new todo list using the provided details and returns the ID of the created list."
    )]
    public static async Task<Results<Created<int>, ProblemHttpResult>> CreateTodoList(
        ISender sender,
        CreateTodoListCommand command
    )
    {
        var result = await sender.Send(command);

        return result.IsSuccess
            ? TypedResults.Created($"/{nameof(TodoLists)}/{result.Value}", result.Value)
            : result.ToProblemHttpResult();
    }

    [EndpointSummary("Update a Todo List")]
    [EndpointDescription(
        "Updates the specified todo list. The ID in the URL must match the ID in the payload."
    )]
    public static async Task<Results<NoContent, BadRequest, ProblemHttpResult>> UpdateTodoList(
        ISender sender,
        int id,
        UpdateTodoListCommand command
    )
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        var result = await sender.Send(command);

        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemHttpResult();
    }

    [EndpointSummary("Delete a Todo List")]
    [EndpointDescription("Deletes the todo list with the specified ID.")]
    public static async Task<Results<NoContent, ProblemHttpResult>> DeleteTodoList(
        ISender sender,
        int id
    )
    {
        var result = await sender.Send(new DeleteTodoListCommand(id));

        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemHttpResult();
    }
}
