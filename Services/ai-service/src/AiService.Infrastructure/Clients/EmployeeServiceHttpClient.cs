using System.Net.Http.Json;
using System.Text.Json;
using AiService.Application.Abstractions;

namespace AiService.Infrastructure.Clients;

public sealed class EmployeeServiceHttpClient(HttpClient httpClient) : IEmployeeQueryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<WorkingHoursWindowDto>> GetAvailabilityAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            $"/api/employees/{employeeId}/availability?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        var slots = await response.Content.ReadFromJsonAsync<List<AvailableSlotEntryDto>>(JsonOptions, cancellationToken) ?? [];

        return slots
            .Select(s => new WorkingHoursWindowDto(
                new DateTimeOffset(s.Date.ToDateTime(s.StartTime), TimeSpan.Zero),
                new DateTimeOffset(s.Date.ToDateTime(s.EndTime), TimeSpan.Zero)))
            .ToList();
    }

    public async Task<EmployeeSummaryDto?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"/api/employees/{employeeId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<EmployeeEntryDto>(JsonOptions, cancellationToken);
        return dto is null ? null : ToSummary(dto);
    }

    public async Task<IReadOnlyList<EmployeeSummaryDto>> GetQualifiedEmployeesAsync(
        Guid serviceId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"/api/employees?serviceId={serviceId}&page=1", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SearchEmployeesEntryDto>(JsonOptions, cancellationToken);
        return result?.Items.Select(ToSummary).ToList() ?? [];
    }

    public async Task<IReadOnlyList<EmployeeSummaryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("/api/employees?page=1", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SearchEmployeesEntryDto>(JsonOptions, cancellationToken);
        return result?.Items.Select(ToSummary).ToList() ?? [];
    }

    private static EmployeeSummaryDto ToSummary(EmployeeEntryDto dto) => new(
        dto.Id, dto.FirstName, dto.LastName, dto.IsActive, dto.Services.Select(s => s.ServiceId).ToList());

    private sealed record AvailableSlotEntryDto(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);

    private sealed record EmployeeServiceAssignmentEntryDto(Guid ServiceId);

    private sealed record EmployeeEntryDto(
        Guid Id, string FirstName, string LastName, bool IsActive, List<EmployeeServiceAssignmentEntryDto> Services);

    private sealed record SearchEmployeesEntryDto(List<EmployeeEntryDto> Items);
}
