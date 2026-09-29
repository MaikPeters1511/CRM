using AppointmentService.Application.Abstractions;
using AppointmentService.Application.Appointments.IntegrationEvents;
using AppointmentService.Domain.Appointments;
using Wolverine;

namespace AppointmentService.Application.Appointments.Reschedule;

public static class RescheduleAppointmentHandler
{
    public static async Task<RescheduleAppointmentResult> Handle(
        RescheduleAppointmentCommand command,
        IAppointmentRepository repository,
        IEmployeeAvailabilityClient employeeAvailabilityClient,
        TimeProvider timeProvider,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var appointment = await repository.GetByIdAsync(command.AppointmentId, cancellationToken);
        if (appointment is null)
        {
            return RescheduleAppointmentResult.Failure(RescheduleAppointmentStatus.NotFound, ["Appointment not found."]);
        }

        if (command.NewStart >= command.NewEnd)
        {
            return RescheduleAppointmentResult.Failure(
                RescheduleAppointmentStatus.ValidationFailed, ["Start must be before End."]);
        }

        EmployeeAvailabilityLookupResult availability;
        try
        {
            availability = await employeeAvailabilityClient.GetAvailabilityAsync(
                appointment.EmployeeId,
                DateOnly.FromDateTime(command.NewStart.DateTime),
                DateOnly.FromDateTime(command.NewEnd.DateTime),
                cancellationToken);
        }
        catch (Exception)
        {
            return RescheduleAppointmentResult.Failure(
                RescheduleAppointmentStatus.DependencyUnavailable,
                ["employee-service is currently unavailable. Please try again later."]);
        }

        if (!availability.Found)
        {
            return RescheduleAppointmentResult.Failure(
                RescheduleAppointmentStatus.EmployeeNotFound, ["Employee does not exist."]);
        }

        var availableWindows = availability.Slots
            .Select(s => new AvailabilityWindow(
                new DateTimeOffset(s.Date.ToDateTime(s.StartTime), command.NewStart.Offset),
                new DateTimeOffset(s.Date.ToDateTime(s.EndTime), command.NewStart.Offset)))
            .ToList();

        var existingAppointments = await repository.GetOverlappingForEmployeeAsync(
            appointment.EmployeeId, command.NewStart, command.NewEnd, excludeAppointmentId: appointment.Id, cancellationToken);

        var existingSlots = existingAppointments
            .Select(a => new ExistingAppointmentSlot(a.Id, a.Start, a.End))
            .ToList();

        var mutation = appointment.Reschedule(
            command.NewStart, command.NewEnd, availableWindows, existingSlots, command.ModifiedBy, timeProvider.GetUtcNow());

        if (!mutation.IsSuccess)
        {
            var status = mutation.Reason == AppointmentMutationFailureReason.Conflict
                ? RescheduleAppointmentStatus.Conflict
                : RescheduleAppointmentStatus.ValidationFailed;

            return RescheduleAppointmentResult.Failure(status, mutation.Errors);
        }

        await repository.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in appointment.DomainEvents)
        {
            if (domainEvent is AppointmentRescheduled rescheduled)
            {
                await messageBus.PublishAsync(new AppointmentRescheduledIntegrationEvent(
                    rescheduled.AppointmentId, rescheduled.OldStart, rescheduled.OldEnd,
                    rescheduled.NewStart, rescheduled.NewEnd, rescheduled.RescheduledAt));
            }
        }

        appointment.ClearDomainEvents();

        return RescheduleAppointmentResult.Success(AppointmentDto.FromDomain(appointment));
    }
}
