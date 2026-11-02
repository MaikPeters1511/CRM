using EmployeeService.Application.Abstractions;

namespace EmployeeService.Application.Notifications;

public static class GetNotificationsHandler
{
    public static async Task<GetNotificationsResult> Handle(
        GetNotificationsQuery query,
        INotificationRepository repository,
        CancellationToken cancellationToken)
    {
        var notifications = await repository.GetByRecipientAsync(query.RecipientEmployeeId, cancellationToken);

        var items = notifications
            .OrderByDescending(n => n.CreatedAt)
            .Select(NotificationDto.FromDomain)
            .ToList();

        return new GetNotificationsResult(items);
    }
}
