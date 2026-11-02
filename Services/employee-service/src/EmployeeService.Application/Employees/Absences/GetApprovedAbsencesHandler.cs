using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees.Absences;

public static class GetApprovedAbsencesHandler
{
    public static async Task<GetApprovedAbsencesResult> Handle(
        GetApprovedAbsencesQuery query,
        IEmployeeRepository repository,
        CancellationToken cancellationToken)
    {
        var records = await repository.SearchAbsencesAsync(
            AbsenceStatus.Approved, query.From, query.To, query.EmployeeId, cancellationToken);

        var items = records
            .OrderBy(r => r.Absence.From)
            .Select(r => new ApprovedAbsenceDto(
                r.EmployeeId,
                r.Absence.Type,
                r.Absence.From,
                r.Absence.To,
                r.Absence.Id,
                r.EmployeeFirstName,
                r.EmployeeLastName))
            .ToList();

        return new GetApprovedAbsencesResult(items);
    }
}
