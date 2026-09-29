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
/// Response shape for <c>GET /api/appointments</c> (calendar view). Deliberately includes
/// <see cref="Date"/>/<see cref="StartTime"/>/<see cref="EndTime"/> as flat top-level properties
/// IN ADDITION to the full appointment fields: employee-service's pre-existing
/// <c>AppointmentAvailabilityHttpClient</c> (see Services/employee-service) deserializes this
/// exact same list response into its own minimal DTO
/// (<c>{ Date: DateOnly, StartTime: TimeOnly, EndTime: TimeOnly }</c>) via
/// <c>GetFromJsonAsync&lt;List&lt;...&gt;&gt;</c>; System.Text.Json ignores the additional
/// properties on that side, so the pre-existing contract keeps working unmodified while this
/// service's own calendar view still gets the full appointment payload it needs.
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
