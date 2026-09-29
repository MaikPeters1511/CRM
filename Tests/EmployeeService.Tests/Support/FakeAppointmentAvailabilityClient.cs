using EmployeeService.Application.Abstractions;

namespace EmployeeService.Tests.Support;

/// <summary>
/// Stand-in for the not-yet-existing `appointment-service` HTTP client
/// (see IAppointmentAvailabilityClient KNOWN GAP note). Lets tests configure
/// exactly which existing appointments should be "returned" for a given call.
/// </summary>
public sealed class FakeAppointmentAvailabilityClient : IAppointmentAvailabilityClient
{
    private readonly List<ExistingAppointmentSlot> _appointments = [];

    public void Seed(ExistingAppointmentSlot appointment) => _appointments.Add(appointment);

    public Task<IReadOnlyList<ExistingAppointmentSlot>> GetAppointmentsAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExistingAppointmentSlot> result = _appointments
            .Where(a => a.Date >= from && a.Date <= to)
            .ToList();

        return Task.FromResult(result);
    }
}
