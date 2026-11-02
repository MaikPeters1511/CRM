using EmployeeService.Application.Abstractions;

namespace EmployeeService.Application.Employees.ApproveAbsence;

public sealed record ApproveAbsenceCommand(Guid EmployeeId, Guid AbsenceId, string DecidedBy);

public enum ApproveAbsenceStatus
{
    Success,
    NotFound,
    Conflict
}

public sealed record ApproveAbsenceResult(ApproveAbsenceStatus Status, AbsenceDto? Absence, IReadOnlyList<string> Errors)
{
    public static ApproveAbsenceResult Success(AbsenceDto absence) => new(ApproveAbsenceStatus.Success, absence, []);

    public static ApproveAbsenceResult NotFound() => new(ApproveAbsenceStatus.NotFound, null, []);

    public static ApproveAbsenceResult Conflict(IReadOnlyList<string> errors) =>
        new(ApproveAbsenceStatus.Conflict, null, errors);
}
