namespace EmployeeService.Application.Employees.Deactivate;

public sealed record DeactivateEmployeeCommand(Guid EmployeeId, string DeactivatedBy);

public enum DeactivateEmployeeStatus
{
    Success,
    NotFound
}

public sealed record DeactivateEmployeeResult(DeactivateEmployeeStatus Status)
{
    public static DeactivateEmployeeResult Success() => new(DeactivateEmployeeStatus.Success);

    public static DeactivateEmployeeResult NotFound() => new(DeactivateEmployeeStatus.NotFound);
}
