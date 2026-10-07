using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.TodoItems.Commands.CreateTodoItem;
using AnalisisSentimiento.Application.TodoItems.Commands.UpdateTodoItem;
using AnalisisSentimiento.Application.TodoLists.Commands.CreateTodoList;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.FunctionalTests.TodoItems.Commands;

public class UpdateTodoItemTests : TestBase
{
    [Test]
    public async Task ShouldRequireValidTodoItemId()
    {
        var command = new UpdateTodoItemCommand { Id = 99, Title = "New Title" };

        var result = await TestApp.SendAsync(command);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e is NotFoundError);
    }

    [Test]
    public async Task ShouldUpdateTodoItem()
    {
        var userId = await TestApp.RunAsDefaultUserAsync();

        var listId = (
            await TestApp.SendAsync(new CreateTodoListCommand { Title = "New List" })
        ).Value;

        var itemId = (
            await TestApp.SendAsync(
                new CreateTodoItemCommand { ListId = listId, Title = "New Item" }
            )
        ).Value;

        var command = new UpdateTodoItemCommand { Id = itemId, Title = "Updated Item Title" };

        await TestApp.SendAsync(command);

        var item = await TestApp.FindAsync<TodoItem>(itemId);

        item.ShouldNotBeNull();
        item!.Title.ShouldBe(command.Title);
        item.LastModifiedBy.ShouldNotBeNull();
        item.LastModifiedBy.ShouldBe(userId);
        item.LastModified.ShouldBe(DateTime.Now, TimeSpan.FromMilliseconds(10000));
    }
}
