using CustomerService.Application.Abstractions;

namespace CustomerService.Application.Customers.Search;

public static class SearchCustomersHandler
{
    public static async Task<SearchCustomersResult> Handle(
        SearchCustomersQuery query,
        ICustomerRepository repository,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        var searchResult = await repository.SearchAsync(query.Search, query.IsActive, page, pageSize, cancellationToken);

        var items = searchResult.Items.Select(CustomerDto.FromDomain).ToList();

        return new SearchCustomersResult(items, searchResult.TotalCount, page, pageSize);
    }
}
