using EmployeeService.Application.Abstractions;

namespace EmployeeService.Application.Employees.Search;

public static class SearchEmployeesHandler
{
    public static async Task<SearchEmployeesResult> Handle(
        SearchEmployeesQuery query,
        IEmployeeRepository repository,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        var searchResult = await repository.SearchAsync(
            query.Search, query.Role, query.ServiceId, page, pageSize, cancellationToken);

        var items = searchResult.Items.Select(EmployeeDto.FromDomain).ToList();

        return new SearchEmployeesResult(items, searchResult.TotalCount, page, pageSize);
    }
}
