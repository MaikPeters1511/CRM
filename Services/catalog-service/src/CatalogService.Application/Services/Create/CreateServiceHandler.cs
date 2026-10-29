using CatalogService.Application.Abstractions;
using CatalogService.Domain.Services;

namespace CatalogService.Application.Services.Create;

public static class CreateServiceHandler
{
    public static async Task<CreateServiceResult> Handle(
        CreateServiceCommand command,
        IServiceRepository repository,
        TimeProvider timeProvider,
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

        await repository.AddAsync(result.Service!, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return CreateServiceResult.Success(ServiceDto.FromDomain(result.Service!));
    }
}
