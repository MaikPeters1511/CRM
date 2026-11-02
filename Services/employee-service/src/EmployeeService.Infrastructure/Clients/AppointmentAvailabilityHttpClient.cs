using System.Net.Http.Json;
using EmployeeService.Application.Abstractions;

namespace EmployeeService.Infrastructure.Clients;

/// <summary>
/// Synchronous HTTP client towards `appointment-service` (ADR 0001, Abschnitt 2/5).
///
/// The route/response-shape below ("GET /api/appointments?employeeId=&amp;from=&amp;to=")
/// reflects the contract described in the spec ("Liste bestehender Termine im Zeitraum X-Y für
/// Mitarbeiter Z"). Since abwesenheit-workflow.openspec.md (Abschnitt 5/7), the response is a
/// wrapper object <c>{ appointments: [...], absenceEntries: [...] }</c> instead of a bare array
/// (see AppointmentService.Web.Controllers.AppointmentsController.GetCalendar) - this client
/// only reads the <c>appointments</c> property (<see cref="AppointmentListResponse"/>) and
/// ignores <c>absenceEntries</c>, which is irrelevant for the conflict-check/availability use
/// case here.
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

        var response = await httpClient.GetFromJsonAsync<AppointmentListResponse>(requestUri, cancellationToken);

        if (response?.Appointments is null)
        {
            return [];
        }

        return response.Appointments
            .Select(r => new ExistingAppointmentSlot(r.Date, r.StartTime, r.EndTime))
            .ToList();
    }

    /// <summary>Wrapper matching appointment-service's `GET /api/appointments` response shape.</summary>
    private sealed record AppointmentListResponse(List<AppointmentSlotResponse> Appointments);

    /// <summary>Placeholder DTO matching the assumed appointment-service response shape.</summary>
    private sealed record AppointmentSlotResponse(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);
}
