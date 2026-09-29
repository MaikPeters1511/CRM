namespace AppointmentService.Application.Appointments.Cancel;

public sealed record CancelAppointmentCommand(Guid AppointmentId, string Reason, string CancelledBy);

public enum CancelAppointmentStatus
{
    Success,
    NotFound,
    ValidationFailed,
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
