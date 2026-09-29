using System.Net;
using System.Net.Http.Json;
using AppointmentService.Application.Abstractions;

namespace AppointmentService.Infrastructure.Clients;

/// <summary>
/// Synchronous HTTP client towards <c>employee-service</c>
/// (<c>GET /api/employees/{id}/availability?from=&amp;to=</c>, ADR 0001 Abschnitt 2/5). Resilience
/// (timeout/retry/circuit-breaker) is configured centrally on the underlying <see cref="HttpClient"/>
/// via <see cref="DependencyInjection.AddAppointmentServiceInfrastructure"/>.
/// </summary>
public sealed class EmployeeAvailabilityHttpClient(HttpClient httpClient) : IEmployeeAvailabilityClient
{
    public async Task<EmployeeAvailabilityLookupResult> GetAvailabilityAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var requestUri = $"/api/employees/{employeeId}/availability?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

        var response = await httpClient.GetAsync(requestUri, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return EmployeeAvailabilityLookupResult.NotFound();
        }

        response.EnsureSuccessStatusCode();

        var slots = await response.Content.ReadFromJsonAsync<List<AvailableSlotDto>>(cancellationToken);

        return EmployeeAvailabilityLookupResult.Of(slots ?? []);
    }
}
