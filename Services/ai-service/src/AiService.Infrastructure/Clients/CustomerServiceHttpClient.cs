using System.Net.Http.Json;
using System.Text.Json;
using AiService.Application.Abstractions;

namespace AiService.Infrastructure.Clients;

public sealed class CustomerServiceHttpClient(HttpClient httpClient) : ICustomerQueryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<CustomerSummaryDto>> SearchByNameAsync(string nameHint, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            $"/api/customers?search={Uri.EscapeDataString(nameHint)}&status=true&page=1", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SearchCustomersEntryDto>(JsonOptions, cancellationToken);

        return result?.Items
            .Select(c => new CustomerSummaryDto(c.Id, $"{c.FirstName} {c.LastName}"))
            .ToList() ?? [];
    }

    // Requirement "Datenminimierung bei externen LLM-Aufrufen": absichtlich nur Id/Name - keine
    // Adresse/Kontaktdaten/Historie aus CustomerDto werden hierher gemappt.
    private sealed record CustomerEntryDto(Guid Id, string FirstName, string LastName);

    private sealed record SearchCustomersEntryDto(List<CustomerEntryDto> Items);
}
