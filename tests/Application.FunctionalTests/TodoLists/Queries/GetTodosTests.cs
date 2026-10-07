using AnalisisSentimiento.Application.TodoLists.Queries.GetTodos;
using AnalisisSentimiento.Domain.Entities;
using AnalisisSentimiento.Domain.ValueObjects;

namespace AnalisisSentimiento.Application.FunctionalTests.TodoLists.Queries;

public class GetTodosTests : TestBase
{
    [Test]
    public async Task ShouldReturnPriorityLevels()
    {
        await TestApp.RunAsDefaultUserAsync();

        var query = new GetTodosQuery();

        var result = await TestApp.SendAsync(query);

        result.PriorityLevels.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ShouldReturnAllListsAndItems()
    {
        await TestApp.RunAsDefaultUserAsync();

        var shopping = TodoList.Create("Shopping", Colour.Blue);

        shopping.AddItem("Apples", done: true);
        shopping.AddItem("Milk", done: true);
        shopping.AddItem("Bread", done: true);
        shopping.AddItem("Toilet paper");
        shopping.AddItem("Pasta");
        shopping.AddItem("Tissues");
        shopping.AddItem("Tuna");

        await TestApp.AddAsync(shopping);

        var query = new GetTodosQuery();

        var result = await TestApp.SendAsync(query);

        result.Lists.Count.ShouldBe(1);
        result.Lists.First().Items.Count.ShouldBe(7);
    }

    [Test]
    public async Task ShouldDenyAnonymousUser()
    {
        var query = new GetTodosQuery();

        var action = () => TestApp.SendAsync(query);

        await Should.ThrowAsync<UnauthorizedAccessException>(action);
    }
}
