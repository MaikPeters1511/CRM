using CustomerService.Application.Abstractions;
using CustomerService.Domain.Customers;

namespace CustomerService.Tests.Support;

public sealed class FakeCustomerRepository : ICustomerRepository
{
    private readonly List<Customer> _customers = [];

    public IReadOnlyList<Customer> Customers => _customers;

    public int SaveChangesCallCount { get; private set; }

    public void Seed(Customer customer) => _customers.Add(customer);

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_customers.FirstOrDefault(c => c.Id == id));
    }

    public Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        _customers.Add(customer);
        return Task.CompletedTask;
    }

    public Task<CustomerSearchResult> SearchAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IEnumerable<Customer> query = _customers;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                c.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.LastName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (c.Email is not null && c.Email.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (c.Phone is not null && c.Phone.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        var all = query.ToList();
        var page_ = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new CustomerSearchResult(page_, all.Count));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
