using EmployeeService.Application.Abstractions;

namespace EmployeeService.Application.Employees.RejectAbsence;

public sealed record RejectAbsenceCommand(Guid EmployeeId, Guid AbsenceId, string DecidedBy);

public enum RejectAbsenceStatus
{
    Success,
    NotFound,
    Conflict
}

public sealed record RejectAbsenceResult(RejectAbsenceStatus Status, AbsenceDto? Absence, IReadOnlyList<string> Errors)
{
    public static RejectAbsenceResult Success(AbsenceDto absence) => new(RejectAbsenceStatus.Success, absence, []);

    public static RejectAbsenceResult NotFound() => new(RejectAbsenceStatus.NotFound, null, []);

    public static RejectAbsenceResult Conflict(IReadOnlyList<string> errors) =>
        new(RejectAbsenceStatus.Conflict, null, errors);
}
