namespace CatalogService.Domain.Services;

public enum ServiceCatalogChangeType
{
    Created,
    Updated,
    Deactivated,
}

/// <summary>
/// Domain event, raised whenever a catalog entry is created, edited or deactivated
/// (docs/adr/0003-ai-service-qdrant-index-strategie.md, Abschnitt 2/Folgeaufgaben):
/// ai-service consumes the resulting integration event to keep its Qdrant availability
/// index in sync. Carries the full current state so the Application layer does not need
/// to re-read the aggregate when mapping to the integration event.
/// </summary>
public sealed record ServiceCatalogEntryChanged(
    Guid ServiceId,
    string Name,
    string? Description,
    int? DefaultDurationMinutes,
    bool IsActive,
    ServiceCatalogChangeType ChangeType,
    DateTimeOffset ChangedAt);
