namespace CustomerService.Application.Customers.Deactivate;

public sealed record DeactivateCustomerCommand(Guid CustomerId, string DeactivatedBy);

public enum DeactivateCustomerStatus
{
    Success,
    NotFound
}

public sealed record DeactivateCustomerResult(DeactivateCustomerStatus Status)
{
    public static DeactivateCustomerResult Success() => new(DeactivateCustomerStatus.Success);

    public static DeactivateCustomerResult NotFound() => new(DeactivateCustomerStatus.NotFound);
}
