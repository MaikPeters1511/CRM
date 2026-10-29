using CatalogService.Application.Abstractions;

namespace CatalogService.Application.Services.Deactivate;

public static class DeactivateServiceHandler
{
    public static async Task<DeactivateServiceResult> Handle(
        DeactivateServiceCommand command,
        IServiceRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var service = await repository.GetByIdAsync(command.ServiceId, cancellationToken);
        if (service is null)
        {
            return DeactivateServiceResult.NotFound();
        }

        service.Deactivate(command.DeactivatedBy, timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken);

        return DeactivateServiceResult.Success();
    }
}
