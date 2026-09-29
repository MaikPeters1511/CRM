namespace CustomerService.Domain.Customers;

public sealed class CustomerMutationResult
{
    public Customer? Customer { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsSuccess => Errors.Count == 0;

    private CustomerMutationResult(Customer? customer, IReadOnlyList<string> errors)
    {
        Customer = customer;
        Errors = errors;
    }

    public static CustomerMutationResult Success(Customer customer) => new(customer, []);

    public static CustomerMutationResult Failure(IReadOnlyList<string> errors) => new(null, errors);
}
