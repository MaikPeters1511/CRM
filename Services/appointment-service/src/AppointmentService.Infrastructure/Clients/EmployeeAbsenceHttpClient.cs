using System.Net.Http.Json;
using AppointmentService.Application.Abstractions;

namespace AppointmentService.Infrastructure.Clients;

/// <summary>
/// Synchronous HTTP client towards <c>employee-service</c>
/// (<c>GET /api/employees/absences/approved?from=&amp;to=&amp;employeeId=</c>,
/// abwesenheit-workflow.openspec.md Abschnitt 5, ADR 0001 Abschnitt 2/5). Resilience
/// (timeout/retry/circuit-breaker) is configured centrally on the underlying <see cref="HttpClient"/>
/// via <see cref="DependencyInjection.AddAppointmentServiceInfrastructure"/>, identical pattern to
/// <see cref="EmployeeAvailabilityHttpClient"/>.
/// </summary>
public sealed class EmployeeAbsenceHttpClient(HttpClient httpClient) : IEmployeeAbsenceClient
{
    public async Task<IReadOnlyList<ApprovedAbsenceDto>> GetApprovedAbsencesAsync(
        Guid? employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var requestUri = $"/api/employees/absences/approved?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        if (employeeId.HasValue)
        {
            requestUri += $"&employeeId={employeeId.Value}";
        }

        var response = await httpClient.GetFromJsonAsync<List<ApprovedAbsenceDto>>(requestUri, cancellationToken);

        return response ?? [];
    }
}
