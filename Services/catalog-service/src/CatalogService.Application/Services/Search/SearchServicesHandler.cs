using CatalogService.Application.Abstractions;

namespace CatalogService.Application.Services.Search;

public static class SearchServicesHandler
{
    public static async Task<SearchServicesResult> Handle(
        SearchServicesQuery query,
        IServiceRepository repository,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        var searchResult = await repository.SearchAsync(query.Search, query.IsActive, page, pageSize, cancellationToken);

        var items = searchResult.Items.Select(ServiceDto.FromDomain).ToList();

        return new SearchServicesResult(items, searchResult.TotalCount, page, pageSize);
    }
}
