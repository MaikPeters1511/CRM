using CustomerService.Application.Abstractions;
using CustomerService.Domain.Customers;

namespace CustomerService.Application.Customers.Create;

public static class CreateCustomerHandler
{
    public static async Task<CreateCustomerResult> Handle(
        CreateCustomerCommand command,
        ICustomerRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var address = command.Address is null
            ? null
            : Address.Create(command.Address.Street, command.Address.City, command.Address.ZipCode, command.Address.Country);

        var result = Customer.Create(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Phone,
            address,
            command.CreatedBy,
            timeProvider.GetUtcNow());

        if (!result.IsSuccess)
        {
            return CreateCustomerResult.Failure(result.Errors);
        }

        await repository.AddAsync(result.Customer!, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return CreateCustomerResult.Success(CustomerDto.FromDomain(result.Customer!));
    }
}
