namespace AppointmentService.Application.Appointments.Cancel;

/// <summary>
/// <paramref name="IsAdmin"/>/<paramref name="CurrentEmployeeId"/> carry the caller's authorization
/// context (terminverwaltung.openspec.md, Abschnitt 3 "Sichtbarkeit von Terminen nach Rolle (RBAC)").
/// Role <c>Employee</c> may only cancel appointments assigned to their own <c>EmployeeId</c>.
/// </summary>
public sealed record CancelAppointmentCommand(
    Guid AppointmentId, string Reason, string CancelledBy, bool IsAdmin, Guid? CurrentEmployeeId);

public enum CancelAppointmentStatus
{
    Success,
    NotFound,
    ValidationFailed,
    Forbidden,
}

public sealed record CancelAppointmentResult(
    CancelAppointmentStatus Status,
    AppointmentDto? Appointment,
    IReadOnlyList<string> Errors)
{
    public static CancelAppointmentResult Success(AppointmentDto appointment) =>
        new(CancelAppointmentStatus.Success, appointment, []);

    public static CancelAppointmentResult Failure(CancelAppointmentStatus status, IReadOnlyList<string> errors) =>
        new(status, null, errors);
}
