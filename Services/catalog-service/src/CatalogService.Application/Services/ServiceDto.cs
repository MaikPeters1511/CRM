using CatalogService.Domain.Services;

namespace CatalogService.Application.Services;

public sealed record ServiceDto(
    Guid Id,
    string Name,
    string? Description,
    int? DefaultDurationMinutes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy)
{
    public static ServiceDto FromDomain(Service service) => new(
        service.Id,
        service.Name,
        service.Description,
        service.DefaultDurationMinutes,
        service.IsActive,
        service.CreatedAt,
        service.CreatedBy,
        service.ModifiedAt,
        service.ModifiedBy);
}
