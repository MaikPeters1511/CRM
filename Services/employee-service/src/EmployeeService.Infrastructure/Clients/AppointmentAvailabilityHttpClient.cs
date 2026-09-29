using System.Net.Http.Json;
using EmployeeService.Application.Abstractions;

namespace EmployeeService.Infrastructure.Clients;

/// <summary>
/// Synchronous HTTP client towards `appointment-service` (ADR 0001, Abschnitt 2/5).
///
/// KNOWN GAP: `appointment-service` does not exist yet. The route/response-shape below
/// ("GET /api/appointments?employeeId=&amp;from=&amp;to=") reflects the minimal contract
/// described in the spec ("Liste bestehender Termine im Zeitraum X-Y für Mitarbeiter Z").
/// Once `appointment-service` is implemented, only this class (and
/// <see cref="AppointmentSlotResponse"/>) needs to be adjusted to match its real contract -
/// callers depend solely on <see cref="IAppointmentAvailabilityClient"/>.
///
/// Resilience (timeout/retry/circuit-breaker) is configured centrally for the underlying
/// HttpClient via <see cref="DependencyInjection.AddEmployeeServiceInfrastructure"/> using
/// Microsoft.Extensions.Http.Resilience (Polly-based), per ADR 0001 Abschnitt 5.
/// </summary>
public sealed class AppointmentAvailabilityHttpClient(HttpClient httpClient) : IAppointmentAvailabilityClient
{
    public async Task<IReadOnlyList<ExistingAppointmentSlot>> GetAppointmentsAsync(
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var requestUri = $"/api/appointments?employeeId={employeeId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

        var response = await httpClient.GetFromJsonAsync<List<AppointmentSlotResponse>>(requestUri, cancellationToken);

        if (response is null)
        {
            return [];
        }

        return response
            .Select(r => new ExistingAppointmentSlot(r.Date, r.StartTime, r.EndTime))
            .ToList();
    }

    /// <summary>Placeholder DTO matching the assumed appointment-service response shape.</summary>
    private sealed record AppointmentSlotResponse(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);
}
