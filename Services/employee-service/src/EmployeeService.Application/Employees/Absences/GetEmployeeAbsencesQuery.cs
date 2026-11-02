namespace EmployeeService.Application.Employees.Absences;

/// <summary>GET /api/employees/{id}/absences - eigene Anfragen inkl. Status (Ownership-Check in der Web-Schicht).</summary>
public sealed record GetEmployeeAbsencesQuery(Guid EmployeeId);

public sealed record GetEmployeeAbsencesResult(bool Found, IReadOnlyList<AbsenceDto> Absences)
{
    public static GetEmployeeAbsencesResult NotFound() => new(false, []);

    public static GetEmployeeAbsencesResult Of(IReadOnlyList<AbsenceDto> absences) => new(true, absences);
}
