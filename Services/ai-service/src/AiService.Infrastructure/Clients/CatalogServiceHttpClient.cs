using System.Net.Http.Json;
using System.Text.Json;
using AiService.Application.Abstractions;

namespace AiService.Infrastructure.Clients;

public sealed class CatalogServiceHttpClient(HttpClient httpClient) : ICatalogQueryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<ServiceSummaryDto>> SearchActiveServicesAsync(
        string searchTerm, CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(searchTerm)
            ? "/api/services?status=true&page=1"
            : $"/api/services?search={Uri.EscapeDataString(searchTerm)}&status=true&page=1";

        var response = await httpClient.GetAsync(query, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SearchServicesEntryDto>(JsonOptions, cancellationToken);

        return result?.Items
            .Select(s => new ServiceSummaryDto(s.Id, s.Name, s.Description, s.DefaultDurationMinutes))
            .ToList() ?? [];
    }

    private sealed record ServiceEntryDto(Guid Id, string Name, string? Description, int? DefaultDurationMinutes);

    private sealed record SearchServicesEntryDto(List<ServiceEntryDto> Items);
}
