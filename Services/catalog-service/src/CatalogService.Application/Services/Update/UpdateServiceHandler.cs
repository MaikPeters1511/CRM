using CatalogService.Application.Abstractions;

namespace CatalogService.Application.Services.Update;

public static class UpdateServiceHandler
{
    public static async Task<UpdateServiceResult> Handle(
        UpdateServiceCommand command,
        IServiceRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var service = await repository.GetByIdAsync(command.ServiceId, cancellationToken);
        if (service is null)
        {
            return UpdateServiceResult.NotFound();
        }

        var result = service.Update(
            command.Name,
            command.Description,
            command.DefaultDurationMinutes,
            command.ModifiedBy,
            timeProvider.GetUtcNow());

        if (!result.IsSuccess)
        {
            return UpdateServiceResult.ValidationFailed(result.Errors);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return UpdateServiceResult.Success(ServiceDto.FromDomain(service));
    }
}
