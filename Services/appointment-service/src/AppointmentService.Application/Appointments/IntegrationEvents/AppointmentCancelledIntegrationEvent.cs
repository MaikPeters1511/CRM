namespace AppointmentService.Application.Appointments.IntegrationEvents;

public sealed record AppointmentCancelledIntegrationEvent(
    Guid AppointmentId,
    string Reason,
    DateTimeOffset CancelledAt,
    string CancelledBy);
