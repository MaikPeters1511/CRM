using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees.Absences;

public static class GetPendingAbsencesHandler
{
    public static async Task<GetPendingAbsencesResult> Handle(
        GetPendingAbsencesQuery query,
        IEmployeeRepository repository,
        CancellationToken cancellationToken)
    {
        var records = await repository.SearchAbsencesAsync(AbsenceStatus.Pending, null, null, null, cancellationToken);

        var items = records
            .OrderBy(r => r.Absence.RequestedAt)
            .Select(r => new PendingAbsenceDto(
                r.EmployeeId,
                r.EmployeeFirstName,
                r.EmployeeLastName,
                r.Absence.Id,
                r.Absence.Type,
                r.Absence.From,
                r.Absence.To,
                r.Absence.RequestedBy,
                r.Absence.RequestedAt))
            .ToList();

        return new GetPendingAbsencesResult(items);
    }
}
