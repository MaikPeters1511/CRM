using AppointmentService.Application.Abstractions;
using AppointmentService.Application.Appointments.IntegrationEvents;
using AppointmentService.Domain.Appointments;
using Wolverine;

namespace AppointmentService.Application.Appointments.Cancel;

public static class CancelAppointmentHandler
{
    public static async Task<CancelAppointmentResult> Handle(
        CancelAppointmentCommand command,
        IAppointmentRepository repository,
        TimeProvider timeProvider,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var appointment = await repository.GetByIdAsync(command.AppointmentId, cancellationToken);
        if (appointment is null)
        {
            return CancelAppointmentResult.Failure(CancelAppointmentStatus.NotFound, ["Appointment not found."]);
        }

        // RBAC (terminverwaltung.openspec.md, Abschnitt 3): Employee-Rolle darf nur eigene Termine
        // stornieren; der Termin bleibt in diesem Fall unveraendert.
        if (!command.IsAdmin && appointment.EmployeeId != command.CurrentEmployeeId)
        {
            return CancelAppointmentResult.Failure(
                CancelAppointmentStatus.Forbidden, ["You are not allowed to cancel this appointment."]);
        }

        var mutation = appointment.Cancel(command.Reason, command.CancelledBy, timeProvider.GetUtcNow());
        if (!mutation.IsSuccess)
        {
            return CancelAppointmentResult.Failure(CancelAppointmentStatus.ValidationFailed, mutation.Errors);
        }

        await repository.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in appointment.DomainEvents)
        {
            if (domainEvent is AppointmentCancelled cancelled)
            {
                await messageBus.PublishAsync(new AppointmentCancelledIntegrationEvent(
                    cancelled.AppointmentId, cancelled.Reason, cancelled.CancelledAt, cancelled.CancelledBy));
            }
        }

        appointment.ClearDomainEvents();

        return CancelAppointmentResult.Success(AppointmentDto.FromDomain(appointment));
    }
}
