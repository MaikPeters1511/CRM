namespace AppointmentService.Domain.Appointments;

public sealed record AppointmentRescheduled(
    Guid AppointmentId,
    DateTimeOffset OldStart,
    DateTimeOffset OldEnd,
    DateTimeOffset NewStart,
    DateTimeOffset NewEnd,
    DateTimeOffset RescheduledAt);
