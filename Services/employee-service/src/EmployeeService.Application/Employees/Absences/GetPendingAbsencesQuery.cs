using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees.Absences;

/// <summary>GET /api/employees/absences/pending (Rolle Admin) - alle ausstehenden Anfragen ueber alle Mitarbeiter.</summary>
public sealed record GetPendingAbsencesQuery;

public sealed record PendingAbsenceDto(
    Guid EmployeeId,
    string EmployeeFirstName,
    string EmployeeLastName,
    Guid AbsenceId,
    AbsenceType Type,
    DateOnly From,
    DateOnly To,
    string RequestedBy,
    DateTimeOffset RequestedAt);

public sealed record GetPendingAbsencesResult(IReadOnlyList<PendingAbsenceDto> Absences);
