using AppointmentService.Application.Abstractions;

namespace AppointmentService.Application.Appointments.GetById;

public static class GetAppointmentByIdHandler
{
    public static async Task<GetAppointmentByIdResult> Handle(
        GetAppointmentByIdQuery query,
        IAppointmentRepository repository,
        CancellationToken cancellationToken)
    {
        var appointment = await repository.GetByIdAsync(query.AppointmentId, cancellationToken);

        return new GetAppointmentByIdResult(appointment is null ? null : AppointmentDto.FromDomain(appointment));
    }
}
