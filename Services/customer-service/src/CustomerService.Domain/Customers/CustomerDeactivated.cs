namespace CustomerService.Domain.Customers;

public sealed record CustomerDeactivated(Guid CustomerId, DateTimeOffset DeactivatedAt, string DeactivatedBy);
