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
