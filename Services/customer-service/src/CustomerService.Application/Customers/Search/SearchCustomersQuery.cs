namespace CustomerService.Application.Customers.Search;

public sealed record SearchCustomersQuery(string? Search, bool? IsActive, int Page = 1, int PageSize = 20);

public sealed record SearchCustomersResult(IReadOnlyList<CustomerDto> Items, int TotalCount, int Page, int PageSize);
