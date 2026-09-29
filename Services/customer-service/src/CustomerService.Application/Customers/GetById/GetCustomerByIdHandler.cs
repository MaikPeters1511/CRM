using CustomerService.Application.Abstractions;

namespace CustomerService.Application.Customers.GetById;

public static class GetCustomerByIdHandler
{
    public static async Task<GetCustomerByIdResult> Handle(
        GetCustomerByIdQuery query,
        ICustomerRepository repository,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetByIdAsync(query.CustomerId, cancellationToken);

        return new GetCustomerByIdResult(customer is null ? null : CustomerDto.FromDomain(customer));
    }
}
