using CatalogService.Domain.Common;

namespace CatalogService.Domain.Services;

/// <summary>
/// Leistungskatalog-Eintrag (UserStories/leistungskatalog.openspec.md Abschnitt 5/7). Bewusst ohne
/// Domain-Event-Publishing (kein Konsument fuer "ServiceDeactivated" vorhanden; "ServiceId"/"ServiceName"
/// bleiben in employee-service als Snapshot-Werte - siehe Proposal, Abschnitt "Impact") - kein Gold-Plating.
/// </summary>
public sealed class Service : Entity
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
