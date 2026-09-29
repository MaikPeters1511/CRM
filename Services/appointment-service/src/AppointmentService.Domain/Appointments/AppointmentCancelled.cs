namespace AppointmentService.Domain.Appointments;

public sealed record AppointmentCancelled(
    Guid AppointmentId,
    string Reason,
    DateTimeOffset CancelledAt,
    string CancelledBy);
