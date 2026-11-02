using EmployeeService.Application.Abstractions;

namespace EmployeeService.Application.Employees.Absences;

public static class GetEmployeeAbsencesHandler
{
    public static async Task<GetEmployeeAbsencesResult> Handle(
        GetEmployeeAbsencesQuery query,
        IEmployeeRepository repository,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(query.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return GetEmployeeAbsencesResult.NotFound();
        }

        var absences = employee.Absences
            .OrderByDescending(a => a.RequestedAt)
            .Select(AbsenceDto.FromDomain)
            .ToList();

        return GetEmployeeAbsencesResult.Of(absences);
    }
}
