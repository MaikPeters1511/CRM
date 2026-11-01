using CatalogService.Domain.Services;

namespace CatalogService.Application.IntegrationEvents;

public sealed record ServiceCatalogChangedIntegrationEvent(
    Guid ServiceId,
    string Name,
    string? Description,
    int? DefaultDurationMinutes,
    bool IsActive,
    ServiceCatalogChangeType ChangeType,
    DateTimeOffset ChangedAt);
