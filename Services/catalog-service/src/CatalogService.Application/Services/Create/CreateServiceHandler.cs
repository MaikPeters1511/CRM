using CatalogService.Application.Abstractions;
using CatalogService.Application.IntegrationEvents;
using CatalogService.Domain.Services;
using Wolverine;

namespace CatalogService.Application.Services.Create;

public static class CreateServiceHandler
{
    public static async Task<CreateServiceResult> Handle(
        CreateServiceCommand command,
        IServiceRepository repository,
        TimeProvider timeProvider,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var result = Service.Create(
            command.Name,
            command.Description,
            command.DefaultDurationMinutes,
            command.CreatedBy,
            timeProvider.GetUtcNow());

        if (!result.IsSuccess)
        {
            return CreateServiceResult.Failure(result.Errors);
        }

        var service = result.Service!;

        await repository.AddAsync(service, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await PublishCatalogChangedEvents.Publish(service, messageBus);

        return CreateServiceResult.Success(ServiceDto.FromDomain(service));
    }
}
