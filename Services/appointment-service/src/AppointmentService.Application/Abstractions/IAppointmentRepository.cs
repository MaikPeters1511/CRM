using AppointmentService.Domain.Appointments;

namespace AppointmentService.Application.Abstractions;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Appointment appointment, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves existing, non-cancelled appointments for <paramref name="employeeId"/> that
    /// overlap the given period - the snapshot handed into the Domain layer's collision check
    /// (terminverwaltung.openspec.md, Abschnitt 7). Excludes <paramref name="excludeAppointmentId"/>
    /// so a reschedule of appointment X does not collide with itself.
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetOverlappingForEmployeeAsync(
        Guid employeeId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Filtered appointment list for the calendar view / <c>GET /api/appointments</c> contract
    /// (terminverwaltung.openspec.md, Abschnitt 5). By default excludes <see cref="AppointmentStatus.Cancelled"/>
    /// appointments, since a cancelled slot is free again and must not block new bookings when this
    /// same endpoint is consumed synchronously by employee-service's availability calculation
    /// (ADR 0001, Abschnitt 2) - see AppointmentAvailabilityHttpClient in employee-service.
    /// </summary>
    Task<IReadOnlyList<Appointment>> SearchAsync(
        Guid? employeeId,
        Guid? customerId,
        DateOnly from,
        DateOnly to,
        bool includeCancelled,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Appointment>> GetFutureConfirmedByCustomerAsync(
        Guid customerId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Appointment>> GetUpcomingConfirmedWithoutReminderAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
