namespace AppointmentService.Domain.Appointments;

/// <summary>
/// Snapshot of one free working-hours window for an employee, as resolved by the Application
/// layer via the synchronous call to <c>employee-service</c> (<c>GET /api/employees/{id}/availability</c>,
/// ADR 0001 Abschnitt 2/5) and handed into the Domain layer as a plain parameter. The Domain layer
/// itself never calls any other service (terminverwaltung.openspec.md, Abschnitt 7).
/// </summary>
public sealed record AvailabilityWindow(DateTimeOffset Start, DateTimeOffset End);
