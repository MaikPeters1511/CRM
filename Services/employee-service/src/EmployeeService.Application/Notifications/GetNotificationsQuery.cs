namespace EmployeeService.Application.Notifications;

/// <summary>GET /api/notifications - eigene Benachrichtigungen, neueste zuerst.</summary>
public sealed record GetNotificationsQuery(Guid RecipientEmployeeId);

public sealed record GetNotificationsResult(IReadOnlyList<NotificationDto> Notifications);
