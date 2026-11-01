using CatalogService.Domain.Services;
using Wolverine;

namespace CatalogService.Application.IntegrationEvents;

/// <summary>
/// Maps <see cref="ServiceCatalogEntryChanged"/> domain events to the outbound
/// <see cref="ServiceCatalogChangedIntegrationEvent"/> (docs/adr/0003, Folgeaufgaben) and clears
/// them afterwards. Shared by Create/Update/Deactivate handlers to avoid duplicating this
/// mapping/publish/clear sequence three times.
/// </summary>
public static class PublishCatalogChangedEvents
{
    public static async Task Publish(Service service, IMessageBus messageBus)
    {
        foreach (var domainEvent in service.DomainEvents)
        {
            if (domainEvent is ServiceCatalogEntryChanged changed)
            {
                await messageBus.PublishAsync(new ServiceCatalogChangedIntegrationEvent(
                    changed.ServiceId, changed.Name, changed.Description, changed.DefaultDurationMinutes,
                    changed.IsActive, changed.ChangeType, changed.ChangedAt));
            }
        }

        service.ClearDomainEvents();
    }
}
