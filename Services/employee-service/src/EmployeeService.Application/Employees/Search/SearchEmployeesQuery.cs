using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees.Search;

public sealed record SearchEmployeesQuery(
    string? Search,
    EmployeeRole? Role,
    Guid? ServiceId,
    int Page = 1,
    int PageSize = 20);

public sealed record SearchEmployeesResult(IReadOnlyList<EmployeeDto> Items, int TotalCount, int Page, int PageSize);
