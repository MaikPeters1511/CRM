namespace AppointmentService.Application.Appointments.GetById;

/// <summary>
/// <paramref name="IsAdmin"/>/<paramref name="CurrentEmployeeId"/> carry the caller's authorization
/// context (terminverwaltung.openspec.md, Abschnitt 3 "Sichtbarkeit von Terminen nach Rolle (RBAC)").
/// For role <c>Employee</c> a lookup of an appointment belonging to another employee SHALL behave as
/// "not found" (existence of other employees' appointments is not disclosed).
/// </summary>
public sealed record GetAppointmentByIdQuery(Guid AppointmentId, bool IsAdmin, Guid? CurrentEmployeeId);

public sealed record GetAppointmentByIdResult(AppointmentDto? Appointment)
{
    public bool Found => Appointment is not null;
}
