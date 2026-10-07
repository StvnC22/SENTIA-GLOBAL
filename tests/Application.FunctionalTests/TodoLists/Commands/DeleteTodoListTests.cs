using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.TodoLists.Commands.CreateTodoList;
using AnalisisSentimiento.Application.TodoLists.Commands.DeleteTodoList;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.FunctionalTests.TodoLists.Commands;

public class DeleteTodoListTests : TestBase
{
    [Test]
    public async Task ShouldRequireValidTodoListId()
    {
        var command = new DeleteTodoListCommand(99);

        var result = await TestApp.SendAsync(command);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e is NotFoundError);
    }

    [Test]
    public async Task ShouldDeleteTodoList()
    {
        var listId = (
            await TestApp.SendAsync(new CreateTodoListCommand { Title = "New List" })
        ).Value;

        await TestApp.SendAsync(new DeleteTodoListCommand(listId));

        var list = await TestApp.FindAsync<TodoList>(listId);

        list.ShouldBeNull();
    }
}
