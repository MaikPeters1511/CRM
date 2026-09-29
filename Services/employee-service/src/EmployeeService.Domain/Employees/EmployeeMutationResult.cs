namespace EmployeeService.Domain.Employees;

public sealed class EmployeeMutationResult
{
    public Employee? Employee { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsSuccess => Errors.Count == 0;

    private EmployeeMutationResult(Employee? employee, IReadOnlyList<string> errors)
    {
        Employee = employee;
        Errors = errors;
    }

    public static EmployeeMutationResult Success(Employee employee) => new(employee, []);

    public static EmployeeMutationResult Failure(IReadOnlyList<string> errors) => new(null, errors);
}
