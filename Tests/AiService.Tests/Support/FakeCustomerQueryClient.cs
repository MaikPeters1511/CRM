using AiService.Application.Abstractions;

namespace AiService.Tests.Support;

public sealed class FakeCustomerQueryClient : ICustomerQueryClient
{
    private readonly List<CustomerSummaryDto> _customers = [];

    public void Seed(CustomerSummaryDto customer) => _customers.Add(customer);

    public Task<IReadOnlyList<CustomerSummaryDto>> SearchByNameAsync(string nameHint, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CustomerSummaryDto>>(
            _customers.Where(c => c.FullName.Contains(nameHint, StringComparison.OrdinalIgnoreCase)).ToList());
}
