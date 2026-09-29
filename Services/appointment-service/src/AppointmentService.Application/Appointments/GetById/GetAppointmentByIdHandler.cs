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

        // RBAC (terminverwaltung.openspec.md, Abschnitt 3): Employee-Rolle darf nur eigene Termine lesen;
        // ein fremder Termin wird wie "nicht gefunden" behandelt (keine Existenzbestaetigung).
        if (appointment is not null && !query.IsAdmin && appointment.EmployeeId != query.CurrentEmployeeId)
        {
            return new GetAppointmentByIdResult(null);
        }

        return new GetAppointmentByIdResult(appointment is null ? null : AppointmentDto.FromDomain(appointment));
    }
}
