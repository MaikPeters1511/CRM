namespace CatalogService.Application.Services.Update;

public sealed record UpdateServiceCommand(
    Guid ServiceId,
    string Name,
    string? Description,
    int? DefaultDurationMinutes,
    string ModifiedBy);

public enum UpdateServiceStatus
{
    Success,
    ValidationFailed,
    NotFound
}

public sealed record UpdateServiceResult(UpdateServiceStatus Status, ServiceDto? Service, IReadOnlyList<string> Errors)
{
    public static UpdateServiceResult Success(ServiceDto service) =>
        new(UpdateServiceStatus.Success, service, []);

    public static UpdateServiceResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(UpdateServiceStatus.ValidationFailed, null, errors);

    public static UpdateServiceResult NotFound() =>
        new(UpdateServiceStatus.NotFound, null, []);
}
