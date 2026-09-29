using CustomerService.Domain.Customers;

namespace CustomerService.Application.Customers;

public sealed record AddressDto(string Street, string City, string ZipCode, string Country);

public sealed record CustomerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    AddressDto? Address,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy)
{
    public static CustomerDto FromDomain(Customer customer) => new(
        customer.Id,
        customer.FirstName,
        customer.LastName,
        customer.Email,
        customer.Phone,
        customer.Address is null
            ? null
            : new AddressDto(customer.Address.Street, customer.Address.City, customer.Address.ZipCode, customer.Address.Country),
        customer.IsActive,
        customer.CreatedAt,
        customer.CreatedBy,
        customer.ModifiedAt,
        customer.ModifiedBy);
}
