namespace EmployeeService.Application.Notifications;

/// <summary>
/// POST /api/notifications/{id}/read. Ownership-Check analog Abwesenheiten: nur der Empfaenger
/// (oder ein Admin) darf eine Benachrichtigung als gelesen markieren.
/// </summary>
public sealed record MarkNotificationAsReadCommand(Guid NotificationId, Guid ActorEmployeeId, bool IsAdmin);

public enum MarkNotificationAsReadStatus
{
    Success,
    NotFound,
    Forbidden
}

public sealed record MarkNotificationAsReadResult(MarkNotificationAsReadStatus Status)
{
    public static MarkNotificationAsReadResult Success() => new(MarkNotificationAsReadStatus.Success);

    public static MarkNotificationAsReadResult NotFound() => new(MarkNotificationAsReadStatus.NotFound);

    public static MarkNotificationAsReadResult Forbidden() => new(MarkNotificationAsReadStatus.Forbidden);
}
