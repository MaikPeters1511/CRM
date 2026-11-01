using CatalogService.Application.Abstractions;
using CatalogService.Application.IntegrationEvents;
using Wolverine;

namespace CatalogService.Application.Services.Deactivate;

public static class DeactivateServiceHandler
{
    public static async Task<DeactivateServiceResult> Handle(
        DeactivateServiceCommand command,
        IServiceRepository repository,
        TimeProvider timeProvider,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var service = await repository.GetByIdAsync(command.ServiceId, cancellationToken);
        if (service is null)
        {
            return DeactivateServiceResult.NotFound();
        }

        service.Deactivate(command.DeactivatedBy, timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken);

        await PublishCatalogChangedEvents.Publish(service, messageBus);

        return DeactivateServiceResult.Success();
    }
}
