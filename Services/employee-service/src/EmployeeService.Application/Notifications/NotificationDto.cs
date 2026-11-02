using EmployeeService.Domain.Notifications;

namespace EmployeeService.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    Guid RecipientEmployeeId,
    string Text,
    string? Link,
    bool IsRead,
    DateTimeOffset CreatedAt)
{
    public static NotificationDto FromDomain(Notification notification) => new(
        notification.Id,
        notification.RecipientEmployeeId,
        notification.Text,
        notification.Link,
        notification.IsRead,
        notification.CreatedAt);
}
