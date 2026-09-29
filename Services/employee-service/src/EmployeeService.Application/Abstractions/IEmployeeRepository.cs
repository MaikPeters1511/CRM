using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Abstractions;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);

    Task AddAsync(Employee employee, CancellationToken cancellationToken);

    Task<EmployeeSearchResult> SearchAsync(
        string? search,
        EmployeeRole? role,
        Guid? serviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record EmployeeSearchResult(IReadOnlyList<Employee> Items, int TotalCount);
