using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.TodoItems.Commands.CreateTodoItem;
using AnalisisSentimiento.Application.TodoItems.Commands.DeleteTodoItem;
using AnalisisSentimiento.Application.TodoLists.Commands.CreateTodoList;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.FunctionalTests.TodoItems.Commands;

public class DeleteTodoItemTests : TestBase
{
    [Test]
    public async Task ShouldRequireValidTodoItemId()
    {
        var command = new DeleteTodoItemCommand(99);

        var result = await TestApp.SendAsync(command);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e is NotFoundError);
    }

    [Test]
    public async Task ShouldDeleteTodoItem()
    {
        var listId = (
            await TestApp.SendAsync(new CreateTodoListCommand { Title = "New List" })
        ).Value;

        var itemId = (
            await TestApp.SendAsync(
                new CreateTodoItemCommand { ListId = listId, Title = "New Item" }
            )
        ).Value;

        await TestApp.SendAsync(new DeleteTodoItemCommand(itemId));

        var item = await TestApp.FindAsync<TodoItem>(itemId);

        item.ShouldBeNull();
    }
}
