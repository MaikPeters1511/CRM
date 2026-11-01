using System.Net.Http.Json;
using System.Text.Json;
using AiService.Application.Abstractions;

namespace AiService.Infrastructure.Clients;

public sealed class AppointmentServiceHttpClient(HttpClient httpClient) : IAppointmentQueryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<BookedSlotDto>> GetBookedSlotsAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            $"/api/appointments?employeeId={employeeId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var appointments = await response.Content.ReadFromJsonAsync<List<AppointmentEntryDto>>(JsonOptions, cancellationToken)
            ?? [];

        return appointments.Select(a => new BookedSlotDto(a.Start, a.End)).ToList();
    }

    private sealed record AppointmentEntryDto(DateTimeOffset Start, DateTimeOffset End);
}
