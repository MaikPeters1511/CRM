namespace AppointmentService.Application.Appointments.IntegrationEvents;

public sealed record AppointmentCreatedIntegrationEvent(
    Guid AppointmentId,
    Guid CustomerId,
    Guid EmployeeId,
    Guid ServiceId,
    DateTimeOffset Start,
    DateTimeOffset End,
    DateTimeOffset CreatedAt);
