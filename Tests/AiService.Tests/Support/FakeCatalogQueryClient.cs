using AiService.Application.Abstractions;

namespace AiService.Tests.Support;

public sealed class FakeCatalogQueryClient : ICatalogQueryClient
{
    private readonly List<ServiceSummaryDto> _services = [];

    public void Seed(ServiceSummaryDto service) => _services.Add(service);

    public Task<IReadOnlyList<ServiceSummaryDto>> SearchActiveServicesAsync(string searchTerm, CancellationToken cancellationToken)
    {
        IReadOnlyList<ServiceSummaryDto> result = string.IsNullOrWhiteSpace(searchTerm)
            ? _services.ToList()
            : _services.Where(s => s.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        return Task.FromResult(result);
    }
}
