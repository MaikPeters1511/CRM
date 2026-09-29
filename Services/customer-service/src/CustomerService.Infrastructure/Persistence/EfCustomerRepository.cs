using CustomerService.Application.Abstractions;
using CustomerService.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Infrastructure.Persistence;

public sealed class EfCustomerRepository(CustomerDbContext dbContext) : ICustomerRepository
{
    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await dbContext.Customers.AddAsync(customer, cancellationToken);
    }

    public async Task<CustomerSearchResult> SearchAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Customer> query = dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                EF.Functions.ILike(c.FirstName, $"%{search}%") ||
                EF.Functions.ILike(c.LastName, $"%{search}%") ||
                (c.Email != null && EF.Functions.ILike(c.Email, $"%{search}%")) ||
                (c.Phone != null && EF.Functions.ILike(c.Phone, $"%{search}%")));
        }

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new CustomerSearchResult(items, totalCount);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
