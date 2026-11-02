using EmployeeService.Application.Abstractions;

namespace EmployeeService.Application.Notifications;

public static class MarkNotificationAsReadHandler
{
    public static async Task<MarkNotificationAsReadResult> Handle(
        MarkNotificationAsReadCommand command,
        INotificationRepository repository,
        CancellationToken cancellationToken)
    {
        var notification = await repository.GetByIdAsync(command.NotificationId, cancellationToken);
        if (notification is null)
        {
            return MarkNotificationAsReadResult.NotFound();
        }

        if (!command.IsAdmin && notification.RecipientEmployeeId != command.ActorEmployeeId)
        {
            return MarkNotificationAsReadResult.Forbidden();
        }

        notification.MarkAsRead();
        await repository.SaveChangesAsync(cancellationToken);

        return MarkNotificationAsReadResult.Success();
    }
}
