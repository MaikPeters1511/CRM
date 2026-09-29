namespace AppointmentService.Application.Appointments.Calendar;

/// <summary>
/// Backs <c>GET /api/appointments?employeeId=&amp;customerId=&amp;from=&amp;to=</c>
/// (terminverwaltung.openspec.md, Abschnitt 5). This is also the exact contract consumed
/// synchronously by employee-service's <c>IAppointmentAvailabilityClient</c> (ADR 0001,
/// Abschnitt 2) - see <see cref="AppointmentListItemDto"/> for details.
/// </summary>
public sealed record GetCalendarQuery(Guid? EmployeeId, Guid? CustomerId, DateOnly From, DateOnly To);

public sealed record GetCalendarResult(IReadOnlyList<AppointmentListItemDto> Appointments);
