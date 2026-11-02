namespace EmployeeService.Domain.Employees;

public enum AbsenceDecisionStatus
{
    Success,
    NotFound,
    Conflict
}

/// <summary>
/// Ergebnis von <see cref="Employee.ApproveAbsence"/>/<see cref="Employee.RejectAbsence"/>.
/// Unterscheidet (anders als <see cref="AbsenceMutationResult"/>) explizit zwischen "Abwesenheit
/// existiert nicht" (<see cref="AbsenceDecisionStatus.NotFound"/>) und "bereits entschieden"
/// (<see cref="AbsenceDecisionStatus.Conflict"/>), da die Application-/Web-Schicht diese Faelle
/// auf unterschiedliche HTTP-Antworten (404 vs. 409) abbilden muss.
/// </summary>
public sealed class AbsenceDecisionResult
{
    public AbsenceDecisionStatus Status { get; }
    public Absence? Absence { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsSuccess => Status == AbsenceDecisionStatus.Success;

    private AbsenceDecisionResult(AbsenceDecisionStatus status, Absence? absence, IReadOnlyList<string> errors)
    {
        Status = status;
        Absence = absence;
        Errors = errors;
    }

    public static AbsenceDecisionResult Success(Absence absence) => new(AbsenceDecisionStatus.Success, absence, []);

    public static AbsenceDecisionResult NotFound() => new(AbsenceDecisionStatus.NotFound, null, []);

    public static AbsenceDecisionResult Conflict(IReadOnlyList<string> errors) =>
        new(AbsenceDecisionStatus.Conflict, null, errors);
}
