namespace EmployeeService.Domain.Employees;

public sealed class AbsenceMutationResult
{
    public Absence? Absence { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsSuccess => Errors.Count == 0;

    private AbsenceMutationResult(Absence? absence, IReadOnlyList<string> errors)
    {
        Absence = absence;
        Errors = errors;
    }

    public static AbsenceMutationResult Success(Absence absence) => new(absence, []);

    public static AbsenceMutationResult Failure(IReadOnlyList<string> errors) => new(null, errors);
}
