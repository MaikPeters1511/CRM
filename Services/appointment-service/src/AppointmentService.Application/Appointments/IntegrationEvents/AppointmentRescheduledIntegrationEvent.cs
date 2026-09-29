namespace AppointmentService.Application.Appointments.IntegrationEvents;

public sealed record AppointmentRescheduledIntegrationEvent(
    Guid AppointmentId,
    DateTimeOffset OldStart,
    DateTimeOffset OldEnd,
    DateTimeOffset NewStart,
    DateTimeOffset NewEnd,
    DateTimeOffset RescheduledAt);
