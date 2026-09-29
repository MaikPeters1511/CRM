namespace CustomerService.Application.Customers.GetById;

public sealed record GetCustomerByIdQuery(Guid CustomerId);

public sealed record GetCustomerByIdResult(CustomerDto? Customer)
{
    public bool Found => Customer is not null;
}
