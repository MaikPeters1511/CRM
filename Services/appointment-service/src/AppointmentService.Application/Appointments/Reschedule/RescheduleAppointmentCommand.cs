namespace AppointmentService.Application.Appointments.Reschedule;

public sealed record RescheduleAppointmentCommand(
    Guid AppointmentId,
    DateTimeOffset NewStart,
    DateTimeOffset NewEnd,
    string ModifiedBy);

public enum RescheduleAppointmentStatus
{
    Success,
    NotFound,
    ValidationFailed,
    Conflict,
    EmployeeNotFound,
    DependencyUnavailable,
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
