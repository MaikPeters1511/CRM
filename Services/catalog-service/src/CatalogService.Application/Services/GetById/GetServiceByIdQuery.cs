namespace CatalogService.Application.Services.GetById;

public sealed record GetServiceByIdQuery(Guid ServiceId);

public sealed record GetServiceByIdResult(ServiceDto? Service)
{
    public bool Found => Service is not null;
}
