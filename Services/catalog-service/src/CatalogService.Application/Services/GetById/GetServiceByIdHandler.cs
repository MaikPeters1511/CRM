using CatalogService.Application.Abstractions;

namespace CatalogService.Application.Services.GetById;

public static class GetServiceByIdHandler
{
    public static async Task<GetServiceByIdResult> Handle(
        GetServiceByIdQuery query,
        IServiceRepository repository,
        CancellationToken cancellationToken)
    {
        var service = await repository.GetByIdAsync(query.ServiceId, cancellationToken);

        return new GetServiceByIdResult(service is null ? null : ServiceDto.FromDomain(service));
    }
}
