using AiService.Application.Abstractions;

namespace AiService.Tests.Support;

public sealed class FakeEmployeeQueryClient : IEmployeeQueryClient
{
    private readonly Dictionary<Guid, EmployeeSummaryDto> _employees = [];
    private readonly Dictionary<Guid, List<WorkingHoursWindowDto>> _availability = [];

    public void Seed(EmployeeSummaryDto employee) => _employees[employee.EmployeeId] = employee;

    public void SeedAvailability(Guid employeeId, WorkingHoursWindowDto window)
    {
        if (!_availability.TryGetValue(employeeId, out var windows))
        {
            windows = [];
            _availability[employeeId] = windows;
        }

        windows.Add(window);
    }

    public Task<IReadOnlyList<WorkingHoursWindowDto>> GetAvailabilityAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorkingHoursWindowDto>>(
            _availability.TryGetValue(employeeId, out var windows) ? windows : []);

    public Task<EmployeeSummaryDto?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken) =>
        Task.FromResult(_employees.GetValueOrDefault(employeeId));

    public Task<IReadOnlyList<EmployeeSummaryDto>> GetQualifiedEmployeesAsync(Guid serviceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EmployeeSummaryDto>>(
            _employees.Values.Where(e => e.ServiceIds.Contains(serviceId)).ToList());

    public Task<IReadOnlyList<EmployeeSummaryDto>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EmployeeSummaryDto>>(_employees.Values.ToList());
}
