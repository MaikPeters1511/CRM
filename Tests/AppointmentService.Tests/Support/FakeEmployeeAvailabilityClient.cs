using AppointmentService.Application.Abstractions;

namespace AppointmentService.Tests.Support;

public sealed class FakeEmployeeAvailabilityClient : IEmployeeAvailabilityClient
{
    private readonly Dictionary<Guid, List<AvailableSlotDto>> _slotsByEmployee = [];
    public bool ThrowOnCall { get; set; }

    public void SeedSlot(Guid employeeId, AvailableSlotDto slot)
    {
        if (!_slotsByEmployee.TryGetValue(employeeId, out var slots))
        {
            slots = [];
            _slotsByEmployee[employeeId] = slots;
        }

        slots.Add(slot);
    }

    public void SeedKnownEmployeeWithoutSlots(Guid employeeId) => _slotsByEmployee.TryAdd(employeeId, []);

    public Task<EmployeeAvailabilityLookupResult> GetAvailabilityAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (ThrowOnCall)
        {
            throw new HttpRequestException("employee-service unavailable (test).");
        }

        if (!_slotsByEmployee.TryGetValue(employeeId, out var slots))
        {
            return Task.FromResult(EmployeeAvailabilityLookupResult.NotFound());
        }

        IReadOnlyList<AvailableSlotDto> result = slots.Where(s => s.Date >= from && s.Date <= to).ToList();
        return Task.FromResult(EmployeeAvailabilityLookupResult.Of(result));
    }
}
