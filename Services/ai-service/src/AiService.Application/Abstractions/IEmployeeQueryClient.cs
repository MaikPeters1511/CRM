namespace AiService.Application.Abstractions;

/// <summary>
/// Synchroner, lesender Aufruf gegen <c>employee-service</c> (ki-terminassistent.openspec.md,
/// Abschnitt 5): Arbeitszeiten (<c>GET /api/employees/{id}/availability</c>) und
/// Leistungszuordnung (<c>GET /api/employees/{id}</c>/<c>GET /api/employees</c>), fuer die harte
/// Verfuegbarkeits-/Qualifikationspruefung (nie allein ueber den Qdrant-Index, docs/adr/0003 Abschnitt 4).
/// </summary>
public interface IEmployeeQueryClient
{
    Task<IReadOnlyList<WorkingHoursWindowDto>> GetAvailabilityAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<EmployeeSummaryDto?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken);

    /// <summary>Aktive Mitarbeiter, die der Leistung <paramref name="serviceId"/> zugeordnet sind.</summary>
    Task<IReadOnlyList<EmployeeSummaryDto>> GetQualifiedEmployeesAsync(
        Guid serviceId, CancellationToken cancellationToken);

    /// <summary>Alle Mitarbeiter (aktiv und inaktiv) - fuer den taeglichen Konsistenz-Sync (ADR 0003, Abschnitt 3).</summary>
    Task<IReadOnlyList<EmployeeSummaryDto>> GetAllAsync(CancellationToken cancellationToken);
}

public sealed record WorkingHoursWindowDto(DateTimeOffset Start, DateTimeOffset End);

public sealed record EmployeeSummaryDto(
    Guid EmployeeId, string FirstName, string LastName, bool IsActive, IReadOnlyList<Guid> ServiceIds);
