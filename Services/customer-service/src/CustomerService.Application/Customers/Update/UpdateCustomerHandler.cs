using CustomerService.Application.Abstractions;
using CustomerService.Domain.Customers;

namespace CustomerService.Application.Customers.Update;

public static class UpdateCustomerHandler
{
    public static async Task<UpdateCustomerResult> Handle(
        UpdateCustomerCommand command,
        ICustomerRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetByIdAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            return UpdateCustomerResult.NotFound();
        }

        var address = command.Address is null
            ? null
            : Address.Create(command.Address.Street, command.Address.City, command.Address.ZipCode, command.Address.Country);

        var result = customer.Update(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Phone,
            address,
            command.ModifiedBy,
            timeProvider.GetUtcNow());

        if (!result.IsSuccess)
        {
            return UpdateCustomerResult.ValidationFailed(result.Errors);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return UpdateCustomerResult.Success(CustomerDto.FromDomain(customer));
    }
}
