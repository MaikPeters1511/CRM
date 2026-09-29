namespace AppointmentService.Application.Appointments.GetById;

public sealed record GetAppointmentByIdQuery(Guid AppointmentId);

public sealed record GetAppointmentByIdResult(AppointmentDto? Appointment)
{
    public bool Found => Appointment is not null;
}
