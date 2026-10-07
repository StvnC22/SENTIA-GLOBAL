namespace AnalisisSentimiento.Domain.Entities;

public class TodoList : BaseAuditableEntity
{
    private readonly List<TodoItem> _items = [];

    private TodoList() { } // Requerido por EF Core; usar TodoList.Create.

    public string? Title { get; private set; }

    public Colour Colour { get; private set; } = Colour.Grey;

    public IReadOnlyCollection<TodoItem> Items => _items.AsReadOnly();

    public static TodoList Create(string? title, Colour colour)
    {
        return new TodoList { Title = title, Colour = colour };
    }

    public void Rename(string? title)
    {
        Title = title;
    }

    public void UpdateColour(Colour colour)
    {
        Colour = colour;
    }

    public TodoItem AddItem(string? title, bool done = false)
    {
        var item = TodoItem.Create(Id, title, done);

        _items.Add(item);

        return item;
    }
}
