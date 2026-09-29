namespace EmployeeService.Application.Employees.SetWorkingHours;

public sealed record SetWorkingHoursCommand(
    Guid EmployeeId,
    IReadOnlyList<WorkingHoursSlotDto> Slots,
    string ModifiedBy);

public enum SetWorkingHoursStatus
{
    Success,
    ValidationFailed,
    NotFound
}

public sealed record SetWorkingHoursResult(SetWorkingHoursStatus Status, EmployeeDto? Employee, IReadOnlyList<string> Errors)
{
    public static SetWorkingHoursResult Success(EmployeeDto employee) =>
        new(SetWorkingHoursStatus.Success, employee, []);

    public static SetWorkingHoursResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(SetWorkingHoursStatus.ValidationFailed, null, errors);

    public static SetWorkingHoursResult NotFound() =>
        new(SetWorkingHoursStatus.NotFound, null, []);
}
