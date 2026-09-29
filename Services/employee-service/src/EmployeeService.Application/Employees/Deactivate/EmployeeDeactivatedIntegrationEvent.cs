namespace EmployeeService.Application.Employees.Deactivate;

public sealed record EmployeeDeactivatedIntegrationEvent(
    Guid EmployeeId,
    DateTimeOffset DeactivatedAt,
    string DeactivatedBy);
