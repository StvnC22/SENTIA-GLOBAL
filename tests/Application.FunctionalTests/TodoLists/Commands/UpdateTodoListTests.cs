using AnalisisSentimiento.Application.Common.Errors;
using AnalisisSentimiento.Application.Common.Exceptions;
using AnalisisSentimiento.Application.TodoLists.Commands.CreateTodoList;
using AnalisisSentimiento.Application.TodoLists.Commands.UpdateTodoList;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.FunctionalTests.TodoLists.Commands;

public class UpdateTodoListTests : TestBase
{
    [Test]
    public async Task ShouldRequireValidTodoListId()
    {
        var command = new UpdateTodoListCommand { Id = 99, Title = "New Title" };

        var result = await TestApp.SendAsync(command);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e is NotFoundError);
    }

    [Test]
    public async Task ShouldRequireUniqueTitle()
    {
        var listId = (
            await TestApp.SendAsync(new CreateTodoListCommand { Title = "New List" })
        ).Value;

        await TestApp.SendAsync(new CreateTodoListCommand { Title = "Other List" });

        var command = new UpdateTodoListCommand { Id = listId, Title = "Other List" };

        var ex = await Should.ThrowAsync<ValidationException>(() => TestApp.SendAsync(command));

        ex.Errors.ShouldContainKey("Title");
        ex.Errors["Title"].ShouldContain("'Title' must be unique.");
    }

    [Test]
    public async Task ShouldUpdateTodoList()
    {
        var userId = await TestApp.RunAsDefaultUserAsync();

        var listId = (
            await TestApp.SendAsync(new CreateTodoListCommand { Title = "New List" })
        ).Value;

        var command = new UpdateTodoListCommand { Id = listId, Title = "Updated List Title" };

        await TestApp.SendAsync(command);

        var list = await TestApp.FindAsync<TodoList>(listId);

        list.ShouldNotBeNull();
        list!.Title.ShouldBe(command.Title);
        list.LastModifiedBy.ShouldNotBeNull();
        list.LastModifiedBy.ShouldBe(userId);
        list.LastModified.ShouldBe(DateTime.Now, TimeSpan.FromMilliseconds(10000));
    }
}
