namespace AppointmentService.Domain.Appointments;

/// <summary>
/// Lifecycle status of an <see cref="Appointment"/> (terminverwaltung.openspec.md, Abschnitt 5).
/// <see cref="RequiresReview"/> is the compensation status from ADR 0001, Abschnitt 3: set when a
/// referenced customer is deactivated (via <c>CustomerDeactivated</c> integration event) AFTER the
/// appointment was already confirmed, instead of an automatic cancellation.
/// </summary>
public enum AppointmentStatus
{
    Confirmed,
    Cancelled,
    Completed,
    RequiresReview,
}
