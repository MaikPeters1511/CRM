namespace AppointmentService.Application.Appointments.Create;

public sealed record CreateAppointmentCommand(
    Guid CustomerId,
    Guid EmployeeId,
    Guid ServiceId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string CreatedBy);

public enum CreateAppointmentStatus
{
    Success,
    ValidationFailed,
    Conflict,
    CustomerNotFound,
    CustomerInactive,
    EmployeeNotFound,
    DependencyUnavailable,
}

public sealed record CreateAppointmentResult(
    CreateAppointmentStatus Status,
    AppointmentDto? Appointment,
    IReadOnlyList<string> Errors)
{
    public static CreateAppointmentResult Success(AppointmentDto appointment) =>
        new(CreateAppointmentStatus.Success, appointment, []);

    public static CreateAppointmentResult Failure(CreateAppointmentStatus status, IReadOnlyList<string> errors) =>
        new(status, null, errors);
}
