using AppointmentService.Application.Abstractions;
using AppointmentService.Application.Appointments.IntegrationEvents;
using AppointmentService.Domain.Appointments;
using Wolverine;

namespace AppointmentService.Application.Appointments.Create;

/// <summary>
/// Orchestrates the "Termin anlegen" use case (terminverwaltung.openspec.md, Abschnitt 3/5):
/// synchronous precondition checks against customer-service/employee-service (ADR 0001,
/// Abschnitt 2), then delegates the collision/working-hours invariant to the Domain aggregate.
/// </summary>
public static class CreateAppointmentHandler
{
    public static async Task<CreateAppointmentResult> Handle(
        CreateAppointmentCommand command,
        IAppointmentRepository repository,
        ICustomerVerificationClient customerVerificationClient,
        IEmployeeAvailabilityClient employeeAvailabilityClient,
        TimeProvider timeProvider,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        if (command.Start >= command.End)
        {
            return CreateAppointmentResult.Failure(
                CreateAppointmentStatus.ValidationFailed, ["Start must be before End."]);
        }

        CustomerVerificationResult customerResult;
        try
        {
            customerResult = await customerVerificationClient.GetCustomerAsync(command.CustomerId, cancellationToken);
        }
        catch (Exception)
        {
            return CreateAppointmentResult.Failure(
                CreateAppointmentStatus.DependencyUnavailable,
                ["customer-service is currently unavailable. Please try again later."]);
        }

        if (!customerResult.Found)
        {
            return CreateAppointmentResult.Failure(
                CreateAppointmentStatus.CustomerNotFound, ["Customer does not exist."]);
        }

        if (!customerResult.IsActive)
        {
            return CreateAppointmentResult.Failure(
                CreateAppointmentStatus.CustomerInactive, ["Customer is deactivated and cannot be booked."]);
        }

        EmployeeAvailabilityLookupResult availability;
        try
        {
            availability = await employeeAvailabilityClient.GetAvailabilityAsync(
                command.EmployeeId,
                DateOnly.FromDateTime(command.Start.DateTime),
                DateOnly.FromDateTime(command.End.DateTime),
                cancellationToken);
        }
        catch (Exception)
        {
            return CreateAppointmentResult.Failure(
                CreateAppointmentStatus.DependencyUnavailable,
                ["employee-service is currently unavailable. Please try again later."]);
        }

        if (!availability.Found)
        {
            return CreateAppointmentResult.Failure(
                CreateAppointmentStatus.EmployeeNotFound, ["Employee does not exist."]);
        }

        var availableWindows = ToAvailabilityWindows(availability.Slots, command.Start.Offset);

        var existingAppointments = await repository.GetOverlappingForEmployeeAsync(
            command.EmployeeId, command.Start, command.End, excludeAppointmentId: null, cancellationToken);

        var existingSlots = existingAppointments
            .Select(a => new ExistingAppointmentSlot(a.Id, a.Start, a.End))
            .ToList();

        var mutation = Appointment.Create(
            command.CustomerId,
            command.EmployeeId,
            command.ServiceId,
            command.Start,
            command.End,
            availableWindows,
            existingSlots,
            command.CreatedBy,
            timeProvider.GetUtcNow());

        if (!mutation.IsSuccess)
        {
            var status = mutation.Reason == AppointmentMutationFailureReason.Conflict
                ? CreateAppointmentStatus.Conflict
                : CreateAppointmentStatus.ValidationFailed;

            return CreateAppointmentResult.Failure(status, mutation.Errors);
        }

        var appointment = mutation.Appointment!;

        await repository.AddAsync(appointment, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in appointment.DomainEvents)
        {
            if (domainEvent is AppointmentCreated created)
            {
                await messageBus.PublishAsync(new AppointmentCreatedIntegrationEvent(
                    created.AppointmentId, created.CustomerId, created.EmployeeId, created.ServiceId,
                    created.Start, created.End, created.CreatedAt));
            }
        }

        appointment.ClearDomainEvents();

        return CreateAppointmentResult.Success(AppointmentDto.FromDomain(appointment));
    }

    private static List<AvailabilityWindow> ToAvailabilityWindows(
        IReadOnlyList<AvailableSlotDto> slots, TimeSpan offset) =>
        slots
            .Select(s => new AvailabilityWindow(
                new DateTimeOffset(s.Date.ToDateTime(s.StartTime), offset),
                new DateTimeOffset(s.Date.ToDateTime(s.EndTime), offset)))
            .ToList();
}
