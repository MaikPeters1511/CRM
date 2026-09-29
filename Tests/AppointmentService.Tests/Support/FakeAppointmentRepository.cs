using AppointmentService.Application.Abstractions;
using AppointmentService.Domain.Appointments;

namespace AppointmentService.Tests.Support;

public sealed class FakeAppointmentRepository : IAppointmentRepository
{
    private readonly List<Appointment> _appointments = [];

    public IReadOnlyList<Appointment> Appointments => _appointments;

    public int SaveChangesCallCount { get; private set; }

    public void Seed(Appointment appointment) => _appointments.Add(appointment);

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_appointments.FirstOrDefault(a => a.Id == id));
    }

    public Task AddAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        _appointments.Add(appointment);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Appointment>> GetOverlappingForEmployeeAsync(
        Guid employeeId, DateTimeOffset start, DateTimeOffset end, Guid? excludeAppointmentId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Appointment> result = _appointments
            .Where(a => a.EmployeeId == employeeId)
            .Where(a => a.Status != AppointmentStatus.Cancelled)
            .Where(a => a.Id != excludeAppointmentId)
            .Where(a => a.Start < end && start < a.End)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Appointment>> SearchAsync(
        Guid? employeeId, Guid? customerId, DateOnly from, DateOnly to, bool includeCancelled, CancellationToken cancellationToken)
    {
        var rangeStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rangeEnd = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        IEnumerable<Appointment> query = _appointments
            .Where(a => a.Start <= rangeEnd && a.End >= rangeStart);

        if (employeeId.HasValue)
        {
            query = query.Where(a => a.EmployeeId == employeeId.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(a => a.CustomerId == customerId.Value);
        }

        if (!includeCancelled)
        {
            query = query.Where(a => a.Status != AppointmentStatus.Cancelled);
        }

        IReadOnlyList<Appointment> result = query.ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Appointment>> GetFutureConfirmedByCustomerAsync(
        Guid customerId, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        IReadOnlyList<Appointment> result = _appointments
            .Where(a => a.CustomerId == customerId)
            .Where(a => a.Status == AppointmentStatus.Confirmed)
            .Where(a => a.Start > asOf)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Appointment>> GetUpcomingConfirmedWithoutReminderAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken)
    {
        IReadOnlyList<Appointment> result = _appointments
            .Where(a => a.Status == AppointmentStatus.Confirmed)
            .Where(a => a.ReminderSentAt is null)
            .Where(a => a.Start >= windowStart && a.Start <= windowEnd)
            .ToList();

        return Task.FromResult(result);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
