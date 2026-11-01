using AiService.Application.Abstractions;

namespace AiService.Application.IndexSync;

/// <summary>
/// Taeglicher, niedrigpriorer Reparatur-Mechanismus (docs/adr/0003-ai-service-qdrant-index-strategie.md,
/// Abschnitt 3) - KEIN Ersatz fuer die Event-Verarbeitung, sondern ein Sicherheitsnetz gegen Drift.
/// Baut den Index vollstaendig aus dem aktuellen Stand der drei Quell-Services neu auf. Derselbe
/// Code-Pfad wird beim Initial-Sync beim ersten Start von ai-service verwendet (ADR Abschnitt 5).
/// </summary>
public sealed record RunIndexConsistencySyncCommand;

public static class IndexConsistencySyncHandler
{
    public static async Task Handle(
        RunIndexConsistencySyncCommand command,
        ICatalogQueryClient catalogClient,
        IEmployeeQueryClient employeeClient,
        IAvailabilityIndexClient indexClient,
        CancellationToken cancellationToken)
    {
        foreach (var service in await catalogClient.SearchActiveServicesAsync(string.Empty, cancellationToken))
        {
            await indexClient.UpsertServiceAsync(
                service.ServiceId, service.Name, service.Description, isActive: true, cancellationToken);
        }

        foreach (var employee in await employeeClient.GetAllAsync(cancellationToken))
        {
            if (employee.IsActive)
            {
                await indexClient.UpsertEmployeeAsync(
                    employee.EmployeeId, $"{employee.FirstName} {employee.LastName}", employee.ServiceIds, cancellationToken);
            }
            else
            {
                await indexClient.RemoveEmployeeAsync(employee.EmployeeId, cancellationToken);
            }
        }
    }
}
