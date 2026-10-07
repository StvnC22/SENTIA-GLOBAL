using AnalisisSentimiento.Application.Common.Exceptions;
using AnalisisSentimiento.Application.TodoItems.Commands.CreateTodoItem;
using AnalisisSentimiento.Application.TodoLists.Commands.CreateTodoList;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.FunctionalTests.TodoItems.Commands;

public class CreateTodoItemTests : TestBase
{
    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        var command = new CreateTodoItemCommand();

        await Should.ThrowAsync<ValidationException>(() => TestApp.SendAsync(command));
    }

    [Test]
    public async Task ShouldCreateTodoItem()
    {
        var userId = await TestApp.RunAsDefaultUserAsync();

        var listId = (
            await TestApp.SendAsync(new CreateTodoListCommand { Title = "New List" })
        ).Value;

        var command = new CreateTodoItemCommand { ListId = listId, Title = "Tasks" };

        var result = await TestApp.SendAsync(command);

        result.IsSuccess.ShouldBeTrue();

        var item = await TestApp.FindAsync<TodoItem>(result.Value);

        item.ShouldNotBeNull();
        item!.ListId.ShouldBe(command.ListId);
        item.Title.ShouldBe(command.Title);
        item.CreatedBy.ShouldBe(userId);
        item.Created.ShouldBe(DateTime.Now, TimeSpan.FromMilliseconds(10000));
        item.LastModifiedBy.ShouldBe(userId);
        item.LastModified.ShouldBe(DateTime.Now, TimeSpan.FromMilliseconds(10000));
    }
}
