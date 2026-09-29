using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees.Create;

public sealed record CreateEmployeeCommand(
    string FirstName,
    string LastName,
    string Email,
    EmployeeRole Role,
    string CreatedBy);

public enum CreateEmployeeStatus
{
    Success,
    ValidationFailed,
    EmailConflict
}

public sealed record CreateEmployeeResult(CreateEmployeeStatus Status, EmployeeDto? Employee, IReadOnlyList<string> Errors)
{
    public static CreateEmployeeResult Success(EmployeeDto employee) =>
        new(CreateEmployeeStatus.Success, employee, []);

    public static CreateEmployeeResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(CreateEmployeeStatus.ValidationFailed, null, errors);

    public static CreateEmployeeResult EmailConflict() =>
        new(CreateEmployeeStatus.EmailConflict, null, ["Email is already assigned to another employee."]);
}
