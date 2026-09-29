namespace AppointmentService.Domain.Appointments;

/// <summary>
/// Snapshot of an already persisted appointment for the same employee, resolved by the
/// Application layer from <c>IAppointmentRepository</c> and handed into the Domain layer as a
/// plain parameter for the collision check invariant (terminverwaltung.openspec.md, Abschnitt 7).
/// </summary>
public sealed record ExistingAppointmentSlot(Guid AppointmentId, DateTimeOffset Start, DateTimeOffset End);
