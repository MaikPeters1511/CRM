using CatalogService.Domain.Common;

namespace CatalogService.Domain.Services;

/// <summary>
/// Leistungskatalog-Eintrag (UserStories/leistungskatalog.openspec.md Abschnitt 5/7). Seit
/// ai-service (docs/adr/0003-ai-service-qdrant-index-strategie.md, Folgeaufgaben) existiert erstmals
/// ein Konsument fuer Aenderungen am Katalog: jede Mutation feuert <see cref="ServiceCatalogEntryChanged"/>,
/// damit die Application-Schicht ein <c>ServiceCatalogChangedIntegrationEvent</c> veroeffentlichen kann.
/// </summary>
public sealed class Service : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int? DefaultDurationMinutes { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private Service()
    {
    }

    private Service(
        Guid id,
        string name,
        string? description,
        int? defaultDurationMinutes,
        string createdBy,
        DateTimeOffset createdAt) : base(id)
    {
        Name = name;
        Description = description;
        DefaultDurationMinutes = defaultDurationMinutes;
        IsActive = true;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public static ServiceMutationResult Create(
        string name,
        string? description,
        int? defaultDurationMinutes,
        string createdBy,
        DateTimeOffset createdAt)
    {
        var errors = Validate(name);
        if (errors.Count > 0)
        {
            return ServiceMutationResult.Failure(errors);
        }

        var service = new Service(
            Guid.NewGuid(),
            name.Trim(),
            NormalizeOrNull(description),
            defaultDurationMinutes,
            createdBy,
            createdAt);

        service.AddDomainEvent(new ServiceCatalogEntryChanged(
            service.Id, service.Name, service.Description, service.DefaultDurationMinutes,
            service.IsActive, ServiceCatalogChangeType.Created, createdAt));

        return ServiceMutationResult.Success(service);
    }

    public ServiceMutationResult Update(
        string name,
        string? description,
        int? defaultDurationMinutes,
        string modifiedBy,
        DateTimeOffset modifiedAt)
    {
        var errors = Validate(name);
        if (errors.Count > 0)
        {
            return ServiceMutationResult.Failure(errors);
        }

        Name = name.Trim();
        Description = NormalizeOrNull(description);
        DefaultDurationMinutes = defaultDurationMinutes;
        ModifiedBy = modifiedBy;
        ModifiedAt = modifiedAt;

        AddDomainEvent(new ServiceCatalogEntryChanged(
            Id, Name, Description, DefaultDurationMinutes, IsActive,
            ServiceCatalogChangeType.Updated, modifiedAt));

        return ServiceMutationResult.Success(this);
    }

    public void Deactivate(string deactivatedBy, DateTimeOffset deactivatedAt)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        ModifiedBy = deactivatedBy;
        ModifiedAt = deactivatedAt;

        AddDomainEvent(new ServiceCatalogEntryChanged(
            Id, Name, Description, DefaultDurationMinutes, IsActive,
            ServiceCatalogChangeType.Deactivated, deactivatedAt));
    }

    private static IReadOnlyList<string> Validate(string name)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Name is required.");
        }

        return errors;
    }

    private static string? NormalizeOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
