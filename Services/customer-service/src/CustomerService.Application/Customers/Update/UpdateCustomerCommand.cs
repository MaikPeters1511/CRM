using CustomerService.Application.Customers;

namespace CustomerService.Application.Customers.Update;

public sealed record UpdateCustomerCommand(
    Guid CustomerId,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    AddressDto? Address,
    string ModifiedBy);

public enum UpdateCustomerStatus
{
    Success,
    ValidationFailed,
    NotFound
}

public sealed record UpdateCustomerResult(UpdateCustomerStatus Status, CustomerDto? Customer, IReadOnlyList<string> Errors)
{
    public static UpdateCustomerResult Success(CustomerDto customer) =>
        new(UpdateCustomerStatus.Success, customer, []);

    public static UpdateCustomerResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(UpdateCustomerStatus.ValidationFailed, null, errors);

    public static UpdateCustomerResult NotFound() =>
        new(UpdateCustomerStatus.NotFound, null, []);
}
