namespace EmployeeService.Domain.Employees;

public sealed record EmployeeDeactivated(Guid EmployeeId, DateTimeOffset DeactivatedAt, string DeactivatedBy);
