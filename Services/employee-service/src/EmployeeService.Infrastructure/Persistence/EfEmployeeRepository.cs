using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace EmployeeService.Infrastructure.Persistence;

public sealed class EfEmployeeRepository(EmployeeDbContext dbContext) : IEmployeeRepository
{
    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await dbContext.Employees.AnyAsync(e => e.Email == email, cancellationToken);
    }

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken)
    {
        await dbContext.Employees.AddAsync(employee, cancellationToken);
    }

    public async Task<EmployeeSearchResult> SearchAsync(
        string? search,
        EmployeeRole? role,
        Guid? serviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Employee> query = dbContext.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e =>
                EF.Functions.ILike(e.FirstName, $"%{search}%") ||
                EF.Functions.ILike(e.LastName, $"%{search}%") ||
                EF.Functions.ILike(e.Email, $"%{search}%"));
        }

        if (role.HasValue)
        {
            query = query.Where(e => e.Role == role.Value);
        }

        if (serviceId.HasValue)
        {
            query = query.Where(e => e.Services.Any(s => s.ServiceId == serviceId.Value));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new EmployeeSearchResult(items, totalCount);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
