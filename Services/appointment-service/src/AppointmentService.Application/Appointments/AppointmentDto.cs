using AppointmentService.Domain.Appointments;

namespace AppointmentService.Application.Appointments;

public sealed record AppointmentDto(
    Guid Id,
    Guid CustomerId,
    Guid EmployeeId,
    Guid ServiceId,
    DateTimeOffset Start,
    DateTimeOffset End,
    AppointmentStatus Status,
    string? CancellationReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy)
{
    public static AppointmentDto FromDomain(Appointment appointment) => new(
        appointment.Id,
        appointment.CustomerId,
        appointment.EmployeeId,
        appointment.ServiceId,
        appointment.Start,
        appointment.End,
        appointment.Status,
        appointment.CancellationReason,
        appointment.CreatedAt,
        appointment.CreatedBy,
        appointment.ModifiedAt,
        appointment.ModifiedBy);
}

/// <summary>
/// Item shape for the <c>appointments</c> array of the <c>GET /api/appointments</c> response
/// (calendar view). Deliberately includes <see cref="Date"/>/<see cref="StartTime"/>/
/// <see cref="EndTime"/> as flat top-level properties IN ADDITION to the full appointment
/// fields: employee-service's <c>AppointmentAvailabilityHttpClient</c> (see
/// Services/employee-service) deserializes this exact same <c>appointments</c> array into its
/// own minimal DTO (<c>{ Date: DateOnly, StartTime: TimeOnly, EndTime: TimeOnly }</c>);
/// System.Text.Json ignores the additional properties on that side, so that contract keeps
/// working unmodified while this service's own calendar view still gets the full appointment
/// payload it needs.
///
/// NOTE (abwesenheit-workflow.openspec.md, Abschnitt 5/7): the top-level response is now a
/// wrapper object <c>{ appointments: [...], absenceEntries: [...] }</c> instead of a bare array -
/// see <c>AppointmentsController.GetCalendar</c> and
/// <c>AppointmentService.Application.Appointments.Calendar.GetCalendarResult</c>.
/// employee-service's <c>AppointmentAvailabilityHttpClient</c> was updated accordingly to read
/// the <c>appointments</c> property instead of expecting a bare array.
/// </summary>
public sealed record AppointmentListItemDto(
    Guid Id,
    Guid CustomerId,
    Guid EmployeeId,
    Guid ServiceId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateTimeOffset Start,
    DateTimeOffset End,
    AppointmentStatus Status,
    string? CancellationReason)
{
    public static AppointmentListItemDto FromDomain(Appointment appointment) => new(
        appointment.Id,
        appointment.CustomerId,
        appointment.EmployeeId,
        appointment.ServiceId,
        DateOnly.FromDateTime(appointment.Start.DateTime),
        TimeOnly.FromDateTime(appointment.Start.DateTime),
        TimeOnly.FromDateTime(appointment.End.DateTime),
        appointment.Start,
        appointment.End,
        appointment.Status,
        appointment.CancellationReason);
}
