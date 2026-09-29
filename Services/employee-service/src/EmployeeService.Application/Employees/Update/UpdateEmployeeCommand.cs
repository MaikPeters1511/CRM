using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees.Update;

public sealed record UpdateEmployeeCommand(
    Guid EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    EmployeeRole Role,
    IReadOnlyList<EmployeeServiceAssignmentDto>? Services,
    string ModifiedBy);

public enum UpdateEmployeeStatus
{
    Success,
    ValidationFailed,
    NotFound
}

public sealed record UpdateEmployeeResult(UpdateEmployeeStatus Status, EmployeeDto? Employee, IReadOnlyList<string> Errors)
{
    public static UpdateEmployeeResult Success(EmployeeDto employee) =>
        new(UpdateEmployeeStatus.Success, employee, []);

    public static UpdateEmployeeResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(UpdateEmployeeStatus.ValidationFailed, null, errors);

    public static UpdateEmployeeResult NotFound() =>
        new(UpdateEmployeeStatus.NotFound, null, []);
}
