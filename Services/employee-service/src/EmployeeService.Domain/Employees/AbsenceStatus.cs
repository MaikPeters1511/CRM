namespace EmployeeService.Domain.Employees;

/// <summary>
/// Freigabestatus einer Abwesenheitsanfrage (abwesenheit-workflow.openspec.md, Abschnitt 5).
/// Eine neu erfasste Abwesenheit startet immer als <see cref="Pending"/> und wird erst nach
/// Admin-Entscheidung fuer Terminverfuegbarkeit/Kalenderanzeige wirksam (<see cref="Approved"/>).
/// </summary>
public enum AbsenceStatus
{
    Pending,
    Approved,
    Rejected
}
