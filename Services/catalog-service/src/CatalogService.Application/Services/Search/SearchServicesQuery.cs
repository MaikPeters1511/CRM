namespace CatalogService.Application.Services.Search;

public sealed record SearchServicesQuery(string? Search, bool? IsActive, int Page = 1, int PageSize = 20);

public sealed record SearchServicesResult(IReadOnlyList<ServiceDto> Items, int TotalCount, int Page, int PageSize);
