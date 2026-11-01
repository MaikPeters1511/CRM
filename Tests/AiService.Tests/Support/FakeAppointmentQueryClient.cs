using AiService.Application.Abstractions;

namespace AiService.Tests.Support;

public sealed class FakeAppointmentQueryClient : IAppointmentQueryClient
{
    private readonly Dictionary<Guid, List<BookedSlotDto>> _bookedSlots = [];

    public void Seed(Guid employeeId, BookedSlotDto slot)
    {
        if (!_bookedSlots.TryGetValue(employeeId, out var slots))
        {
            slots = [];
            _bookedSlots[employeeId] = slots;
        }

        slots.Add(slot);
    }

    public Task<IReadOnlyList<BookedSlotDto>> GetBookedSlotsAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BookedSlotDto>>(
            _bookedSlots.TryGetValue(employeeId, out var slots) ? slots : []);
}
