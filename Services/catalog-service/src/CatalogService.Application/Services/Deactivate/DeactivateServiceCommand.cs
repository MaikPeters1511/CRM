namespace CatalogService.Application.Services.Deactivate;

public sealed record DeactivateServiceCommand(Guid ServiceId, string DeactivatedBy);

public enum DeactivateServiceStatus
{
    Success,
    NotFound
}

public sealed record DeactivateServiceResult(DeactivateServiceStatus Status)
{
    public static DeactivateServiceResult Success() => new(DeactivateServiceStatus.Success);

    public static DeactivateServiceResult NotFound() => new(DeactivateServiceStatus.NotFound);
}
