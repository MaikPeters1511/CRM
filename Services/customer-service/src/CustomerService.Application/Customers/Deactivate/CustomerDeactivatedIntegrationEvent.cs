namespace CustomerService.Application.Customers.Deactivate;

public sealed record CustomerDeactivatedIntegrationEvent(
    Guid CustomerId,
    DateTimeOffset DeactivatedAt,
    string DeactivatedBy);
