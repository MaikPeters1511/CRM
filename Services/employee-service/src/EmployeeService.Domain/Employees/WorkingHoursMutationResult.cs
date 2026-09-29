namespace EmployeeService.Domain.Employees;

public sealed class WorkingHoursMutationResult
{
    public IReadOnlyList<string> Errors { get; }
    public bool IsSuccess => Errors.Count == 0;

    private WorkingHoursMutationResult(IReadOnlyList<string> errors)
    {
        Errors = errors;
    }

    public static WorkingHoursMutationResult Success() => new([]);

    public static WorkingHoursMutationResult Failure(IReadOnlyList<string> errors) => new(errors);
}
