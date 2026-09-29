using AppointmentService.Application.Appointments.Cancel;
using AppointmentService.Application.Appointments.IntegrationEvents;
using AppointmentService.Domain.Appointments;
using AppointmentService.Tests.Support;
using NSubstitute;
using Wolverine;

namespace AppointmentService.Tests.Application;

public class CancelAppointmentHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private static readonly DateTimeOffset Day = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_OnConfirmedAppointment_MarksCancelledAndPublishesEventAndFreesSlot()
    {
        var repository = new FakeAppointmentRepository();
        var employeeId = Guid.NewGuid();
        var appointment = Appointment.Create(
            Guid.NewGuid(), employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11),
            [new AvailabilityWindow(Day.AddHours(9), Day.AddHours(17))], [], "employee-1", Clock.GetUtcNow()).Appointment!;
        repository.Seed(appointment);

        var messageBus = Substitute.For<IMessageBus>();
        var command = new CancelAppointmentCommand(appointment.Id, "Kunde krank", "employee-1");

        var result = await CancelAppointmentHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CancelAppointmentStatus.Success);
        await Assert.That(appointment.Status).IsEqualTo(AppointmentStatus.Cancelled);
        await messageBus.Received(1).PublishAsync(Arg.Any<AppointmentCancelledIntegrationEvent>());

        var overlapping = await repository.GetOverlappingForEmployeeAsync(
            employeeId, Day.AddHours(10), Day.AddHours(11), null, CancellationToken.None);
        await Assert.That(overlapping).IsEmpty();
    }

    [Test]
    public async Task Handle_UnknownAppointmentId_ReturnsNotFound()
    {
        var repository = new FakeAppointmentRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var command = new CancelAppointmentCommand(Guid.NewGuid(), "Grund", "employee-1");

        var result = await CancelAppointmentHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CancelAppointmentStatus.NotFound);
    }

    [Test]
    public async Task Handle_WithoutReason_ReturnsValidationFailed()
    {
        var repository = new FakeAppointmentRepository();
        var appointment = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11),
            [new AvailabilityWindow(Day.AddHours(9), Day.AddHours(17))], [], "employee-1", Clock.GetUtcNow()).Appointment!;
        repository.Seed(appointment);

        var messageBus = Substitute.For<IMessageBus>();
        var command = new CancelAppointmentCommand(appointment.Id, "", "employee-1");

        var result = await CancelAppointmentHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CancelAppointmentStatus.ValidationFailed);
    }
}
