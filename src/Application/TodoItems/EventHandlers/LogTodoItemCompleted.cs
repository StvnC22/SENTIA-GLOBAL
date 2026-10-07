using AnalisisSentimiento.Domain.Events;
using Microsoft.Extensions.Logging;

namespace AnalisisSentimiento.Application.TodoItems.EventHandlers;

public class LogTodoItemCompleted(ILogger<LogTodoItemCompleted> logger)
    : INotificationHandler<TodoItemCompletedEvent>
{
    public Task Handle(TodoItemCompletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "AnalisisSentimiento Domain Event: {DomainEvent}",
            notification.GetType().Name
        );

        return Task.CompletedTask;
    }
}
