namespace AppointmentService.Application.Appointments.Reschedule;

/// <summary>
/// <paramref name="IsAdmin"/>/<paramref name="CurrentEmployeeId"/> carry the caller's authorization
/// context (terminverwaltung.openspec.md, Abschnitt 3 "Sichtbarkeit von Terminen nach Rolle (RBAC)").
/// Role <c>Employee</c> may only reschedule appointments assigned to their own <c>EmployeeId</c>.
/// </summary>
public sealed record RescheduleAppointmentCommand(
    Guid AppointmentId,
    DateTimeOffset NewStart,
    DateTimeOffset NewEnd,
    string ModifiedBy,
    bool IsAdmin,
    Guid? CurrentEmployeeId);

public enum RescheduleAppointmentStatus
{
    Success,
    NotFound,
    ValidationFailed,
    Conflict,
    EmployeeNotFound,
    DependencyUnavailable,
    Forbidden,
}

public sealed record RescheduleAppointmentResult(
    RescheduleAppointmentStatus Status,
    AppointmentDto? Appointment,
    IReadOnlyList<string> Errors)
{
    public static RescheduleAppointmentResult Success(AppointmentDto appointment) =>
        new(RescheduleAppointmentStatus.Success, appointment, []);

    public static RescheduleAppointmentResult Failure(RescheduleAppointmentStatus status, IReadOnlyList<string> errors) =>
        new(status, null, errors);
}
