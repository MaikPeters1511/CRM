using AiService.Application.Abstractions;

namespace AiService.Application.IndexSync;

/// <summary>Entfernt den Mitarbeiter aus dem Index - keine Vorschlaege mehr fuer diese EmployeeId (docs/adr/0003, Abschnitt 2).</summary>
public static class EmployeeDeactivatedConsumer
{
    public static Task Handle(
        EmployeeDeactivatedIntegrationEvent @event, IAvailabilityIndexClient indexClient, CancellationToken cancellationToken) =>
        indexClient.RemoveEmployeeAsync(@event.EmployeeId, cancellationToken);
}
