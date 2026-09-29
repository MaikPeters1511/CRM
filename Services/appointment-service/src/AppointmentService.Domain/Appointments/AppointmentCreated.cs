namespace AppointmentService.Domain.Appointments;

public sealed record AppointmentCreated(
    Guid AppointmentId,
    Guid CustomerId,
    Guid EmployeeId,
    Guid ServiceId,
    DateTimeOffset Start,
    DateTimeOffset End,
    DateTimeOffset CreatedAt);
