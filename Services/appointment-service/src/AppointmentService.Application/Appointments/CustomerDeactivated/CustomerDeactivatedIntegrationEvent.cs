namespace AppointmentService.Application.Appointments.CustomerDeactivated;

/// <summary>
/// Consumed mirror of <c>CustomerService.Application.Customers.Deactivate.CustomerDeactivatedIntegrationEvent</c>
/// (must stay schema-compatible with the publisher in customer-service, ADR 0001 Abschnitt 2/7).
/// </summary>
public sealed record CustomerDeactivatedIntegrationEvent(
    Guid CustomerId,
    DateTimeOffset DeactivatedAt,
    string DeactivatedBy);
