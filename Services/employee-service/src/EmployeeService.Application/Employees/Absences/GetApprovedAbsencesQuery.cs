using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees.Absences;

/// <summary>
/// GET /api/employees/absences/approved?from=&amp;to=&amp;employeeId= - interner Lesezugriff fuer
/// appointment-service (abwesenheit-workflow.openspec.md, Abschnitt 5), aber auch direkt fuer
/// Endnutzer erreichbar (Rolle Employee/Admin, analog bestehendem Availability-Endpoint).
/// </summary>
public sealed record GetApprovedAbsencesQuery(DateOnly? From, DateOnly? To, Guid? EmployeeId);

public sealed record ApprovedAbsenceDto(
    Guid EmployeeId,
    AbsenceType Type,
    DateOnly From,
    DateOnly To,
    Guid AbsenceId = default,
    string EmployeeFirstName = "",
    string EmployeeLastName = "");

public sealed record GetApprovedAbsencesResult(IReadOnlyList<ApprovedAbsenceDto> Absences);
