namespace EmployeeService.Application.Employees.DeleteAbsence;

public sealed record DeleteAbsenceCommand(
    Guid EmployeeId,
    Guid AbsenceId,
    string RemovedBy);

public enum DeleteAbsenceStatus
{
    Success,
    NotFound,
}

public sealed record DeleteAbsenceResult(DeleteAbsenceStatus Status)
{
    public static DeleteAbsenceResult Success() => new(DeleteAbsenceStatus.Success);

    public static DeleteAbsenceResult NotFound() => new(DeleteAbsenceStatus.NotFound);
}
