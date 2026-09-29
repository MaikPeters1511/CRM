using AppointmentService.Application.Abstractions;
using AppointmentService.Application.Appointments.IntegrationEvents;
using AppointmentService.Application.Appointments.Reschedule;
using AppointmentService.Domain.Appointments;
using AppointmentService.Tests.Support;
using NSubstitute;
using Wolverine;

namespace AppointmentService.Tests.Application;

public class RescheduleAppointmentHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private static readonly DateTimeOffset Day = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_ToKollisionsfreiesZeitfenster_UpdatesAppointmentAndPublishesRescheduledEvent()
    {
        var repository = new FakeAppointmentRepository();
        var employeeId = Guid.NewGuid();
        var appointment = Appointment.Create(
            Guid.NewGuid(), employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11),
            [new AvailabilityWindow(Day.AddHours(9), Day.AddHours(17))], [], "employee-1", Clock.GetUtcNow()).Appointment!;
        appointment.ClearDomainEvents();
        repository.Seed(appointment);

        var employeeClient = new FakeEmployeeAvailabilityClient();
        employeeClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));
        var messageBus = Substitute.For<IMessageBus>();

        var command = new RescheduleAppointmentCommand(appointment.Id, Day.AddHours(13), Day.AddHours(14), "employee-1");

        var result = await RescheduleAppointmentHandler.Handle(
            command, repository, employeeClient, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(RescheduleAppointmentStatus.Success);
        await Assert.That(appointment.Start).IsEqualTo(Day.AddHours(13));
        await messageBus.Received(1).PublishAsync(Arg.Any<AppointmentRescheduledIntegrationEvent>());
    }

    [Test]
    public async Task Handle_UnknownAppointmentId_ReturnsNotFound()
    {
        var repository = new FakeAppointmentRepository();
        var employeeClient = new FakeEmployeeAvailabilityClient();
        var messageBus = Substitute.For<IMessageBus>();

        var command = new RescheduleAppointmentCommand(Guid.NewGuid(), Day.AddHours(13), Day.AddHours(14), "employee-1");

        var result = await RescheduleAppointmentHandler.Handle(
            command, repository, employeeClient, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(RescheduleAppointmentStatus.NotFound);
    }

    [Test]
    public async Task Handle_IntoSlotOccupiedByAnotherAppointment_ReturnsConflict()
    {
        var repository = new FakeAppointmentRepository();
        var employeeId = Guid.NewGuid();

        var appointment = Appointment.Create(
            Guid.NewGuid(), employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11),
            [new AvailabilityWindow(Day.AddHours(9), Day.AddHours(17))], [], "employee-1", Clock.GetUtcNow()).Appointment!;
        repository.Seed(appointment);

        var other = Appointment.Create(
            Guid.NewGuid(), employeeId, Guid.NewGuid(), Day.AddHours(13), Day.AddHours(14),
            [new AvailabilityWindow(Day.AddHours(9), Day.AddHours(17))], [], "employee-1", Clock.GetUtcNow()).Appointment!;
        repository.Seed(other);

        var employeeClient = new FakeEmployeeAvailabilityClient();
        employeeClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));
        var messageBus = Substitute.For<IMessageBus>();

        var command = new RescheduleAppointmentCommand(
            appointment.Id, Day.AddHours(13).AddMinutes(30), Day.AddHours(14).AddMinutes(30), "employee-1");

        var result = await RescheduleAppointmentHandler.Handle(
            command, repository, employeeClient, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(RescheduleAppointmentStatus.Conflict);
    }
}
