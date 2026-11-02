using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Abstractions;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);

    Task AddAsync(Employee employee, CancellationToken cancellationToken);

    Task<EmployeeSearchResult> SearchAsync(
        string? search,
        EmployeeRole? role,
        Guid? serviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Flache Projektion aller Abwesenheiten (ueber alle Mitarbeiter hinweg, optional gefiltert)
    /// fuer die Admin-Freigabe-Ansicht (GET .../absences/pending) und den internen Lesezugriff des
    /// appointment-service (GET .../absences/approved?from=&amp;to=&amp;employeeId=,
    /// abwesenheit-workflow.openspec.md Abschnitt 5).
    /// </summary>
    Task<IReadOnlyList<EmployeeAbsenceRecord>> SearchAbsencesAsync(
        AbsenceStatus? status,
        DateOnly? from,
        DateOnly? to,
        Guid? employeeId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record EmployeeSearchResult(IReadOnlyList<Employee> Items, int TotalCount);

public sealed record EmployeeAbsenceRecord(Guid EmployeeId, string EmployeeFirstName, string EmployeeLastName, Absence Absence);
