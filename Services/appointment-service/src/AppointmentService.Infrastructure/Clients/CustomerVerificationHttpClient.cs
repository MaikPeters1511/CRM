using System.Net;
using System.Net.Http.Json;
using AppointmentService.Application.Abstractions;

namespace AppointmentService.Infrastructure.Clients;

/// <summary>
/// Synchronous HTTP client towards <c>customer-service</c> (<c>GET /api/customers/{id}</c>,
/// ADR 0001 Abschnitt 2/5). Resilience (timeout/retry/circuit-breaker) is configured centrally
/// on the underlying <see cref="HttpClient"/> via
/// <see cref="DependencyInjection.AddAppointmentServiceInfrastructure"/>.
/// </summary>
public sealed class CustomerVerificationHttpClient(HttpClient httpClient) : ICustomerVerificationClient
{
    public async Task<CustomerVerificationResult> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"/api/customers/{customerId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return CustomerVerificationResult.NotFound();
        }

        response.EnsureSuccessStatusCode();

        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>(cancellationToken);

        return customer is null
            ? CustomerVerificationResult.NotFound()
            : CustomerVerificationResult.Of(customer.IsActive);
    }

    /// <summary>Minimal projection of CustomerService.Application.Customers.CustomerDto.</summary>
    private sealed record CustomerResponse(Guid Id, bool IsActive);
}
