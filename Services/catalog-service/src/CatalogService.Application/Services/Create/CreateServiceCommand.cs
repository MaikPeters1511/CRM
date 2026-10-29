using CatalogService.Application.Services;

namespace CatalogService.Application.Services.Create;

public sealed record CreateServiceCommand(
    string Name,
    string? Description,
    int? DefaultDurationMinutes,
    string CreatedBy);

public sealed record CreateServiceResult(bool IsSuccess, ServiceDto? Service, IReadOnlyList<string> Errors)
{
    public static CreateServiceResult Success(ServiceDto service) => new(true, service, []);

    public static CreateServiceResult Failure(IReadOnlyList<string> errors) => new(false, null, errors);
}
