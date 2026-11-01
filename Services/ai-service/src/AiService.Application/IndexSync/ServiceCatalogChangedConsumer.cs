using AiService.Application.Abstractions;

namespace AiService.Application.IndexSync;

/// <summary>Aktualisiert das Leistungs-Embedding (Name/Beschreibung/aktiv-Status, docs/adr/0003, Abschnitt 2).</summary>
public static class ServiceCatalogChangedConsumer
{
    public static Task Handle(
        ServiceCatalogChangedIntegrationEvent @event, IAvailabilityIndexClient indexClient, CancellationToken cancellationToken) =>
        @event.ChangeType == ServiceCatalogChangeType.Deactivated
            ? indexClient.RemoveServiceAsync(@event.ServiceId, cancellationToken)
            : indexClient.UpsertServiceAsync(@event.ServiceId, @event.Name, @event.Description, @event.IsActive, cancellationToken);
}
