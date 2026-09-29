using CustomerService.Application.Customers;

namespace CustomerService.Application.Customers.Create;

public sealed record CreateCustomerCommand(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    AddressDto? Address,
    string CreatedBy);

public sealed record CreateCustomerResult(bool IsSuccess, CustomerDto? Customer, IReadOnlyList<string> Errors)
{
    public static CreateCustomerResult Success(CustomerDto customer) => new(true, customer, []);

    public static CreateCustomerResult Failure(IReadOnlyList<string> errors) => new(false, null, errors);
}
