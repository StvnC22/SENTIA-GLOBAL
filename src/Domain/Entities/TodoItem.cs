namespace AnalisisSentimiento.Domain.Entities;

public class TodoItem : BaseAuditableEntity
{
    private TodoItem() { } // Requerido por EF Core; usar TodoItem.Create/TodoList.AddItem.

    public int ListId { get; private set; }

    public string? Title { get; private set; }

    public string? Note { get; private set; }

    public PriorityLevel Priority { get; private set; } = PriorityLevel.None;

    private bool _done;
    public bool Done
    {
        get => _done;
        private set
        {
            if (value && !_done)
            {
                AddDomainEvent(new TodoItemCompletedEvent(this));
            }

            _done = value;
        }
    }

    public TodoList List { get; private set; } = null!;

    public static TodoItem Create(int listId, string? title, bool done = false)
    {
        return new TodoItem
        {
            ListId = listId,
            Title = title,
            Done = done,
        };
    }

    public void Update(string? title, bool done)
    {
        Title = title;
        Done = done;
    }

    public void UpdateDetail(int listId, PriorityLevel priority, string? note)
    {
        ListId = listId;
        Priority = priority;
        Note = note;
    }
}
