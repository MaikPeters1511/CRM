using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Employees;

namespace EmployeeService.Tests.Support;

public sealed class FakeEmployeeRepository : IEmployeeRepository
{
    private readonly List<Employee> _employees = [];

    public IReadOnlyList<Employee> Employees => _employees;

    public int SaveChangesCallCount { get; private set; }

    public void Seed(Employee employee) => _employees.Add(employee);

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_employees.FirstOrDefault(e => e.Id == id));
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return Task.FromResult(_employees.Any(e => string.Equals(e.Email, email, StringComparison.OrdinalIgnoreCase)));
    }

    public Task AddAsync(Employee employee, CancellationToken cancellationToken)
    {
        _employees.Add(employee);
        return Task.CompletedTask;
    }

    public Task<EmployeeSearchResult> SearchAsync(
        string? search,
        EmployeeRole? role,
        Guid? serviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IEnumerable<Employee> query = _employees;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e =>
                e.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.LastName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (role.HasValue)
        {
            query = query.Where(e => e.Role == role.Value);
        }

        if (serviceId.HasValue)
        {
            query = query.Where(e => e.Services.Any(s => s.ServiceId == serviceId.Value));
        }

        var all = query.ToList();
        var page_ = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new EmployeeSearchResult(page_, all.Count));
    }

    public Task<IReadOnlyList<EmployeeAbsenceRecord>> SearchAbsencesAsync(
        AbsenceStatus? status,
        DateOnly? from,
        DateOnly? to,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var flattened = _employees
            .Where(e => !employeeId.HasValue || e.Id == employeeId.Value)
            .SelectMany(e => e.Absences, (e, a) => new EmployeeAbsenceRecord(e.Id, e.FirstName, e.LastName, a));

        if (status.HasValue)
        {
            flattened = flattened.Where(r => r.Absence.Status == status.Value);
        }

        if (from.HasValue)
        {
            flattened = flattened.Where(r => r.Absence.To >= from.Value);
        }

        if (to.HasValue)
        {
            flattened = flattened.Where(r => r.Absence.From <= to.Value);
        }

        IReadOnlyList<EmployeeAbsenceRecord> result = flattened.OrderBy(r => r.Absence.From).ToList();

        return Task.FromResult(result);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
