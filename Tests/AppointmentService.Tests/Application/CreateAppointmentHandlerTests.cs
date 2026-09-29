using AppointmentService.Application.Abstractions;
using AppointmentService.Application.Appointments.Create;
using AppointmentService.Application.Appointments.IntegrationEvents;
using AppointmentService.Domain.Appointments;
using AppointmentService.Tests.Support;
using NSubstitute;
using Wolverine;

namespace AppointmentService.Tests.Application;

public class CreateAppointmentHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private static readonly DateTimeOffset Day = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero); // Monday.

    [Test]
    public async Task Handle_WithinAvailableWindowAndActiveCustomer_PersistsAppointmentAndPublishesCreatedEvent()
    {
        var repository = new FakeAppointmentRepository();
        var customerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var customerClient = new FakeCustomerVerificationClient();
        customerClient.SeedActive(customerId);
        var employeeClient = new FakeEmployeeAvailabilityClient();
        employeeClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));
        var messageBus = Substitute.For<IMessageBus>();

        var command = new CreateAppointmentCommand(
            customerId, employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11), "employee-1");

        var result = await CreateAppointmentHandler.Handle(
            command, repository, customerClient, employeeClient, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CreateAppointmentStatus.Success);
        await Assert.That(result.Appointment!.Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(repository.Appointments).Count().IsEqualTo(1);
        await messageBus.Received(1).PublishAsync(
            Arg.Is<AppointmentCreatedIntegrationEvent>(e => e.CustomerId == customerId && e.EmployeeId == employeeId));
    }

    [Test]
    public async Task Handle_OutsideWorkingHours_ReturnsValidationFailedAndPersistsNothing()
    {
        var repository = new FakeAppointmentRepository();
        var customerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var customerClient = new FakeCustomerVerificationClient();
        customerClient.SeedActive(customerId);
        var employeeClient = new FakeEmployeeAvailabilityClient();
        employeeClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));
        var messageBus = Substitute.For<IMessageBus>();

        var command = new CreateAppointmentCommand(
            customerId, employeeId, Guid.NewGuid(), Day.AddHours(18), Day.AddHours(19), "employee-1");

        var result = await CreateAppointmentHandler.Handle(
            command, repository, customerClient, employeeClient, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CreateAppointmentStatus.ValidationFailed);
        await Assert.That(repository.Appointments).IsEmpty();
        await messageBus.DidNotReceive().PublishAsync(Arg.Any<AppointmentCreatedIntegrationEvent>());
    }

    [Test]
    public async Task Handle_ConflictingWithExistingAppointmentForSameEmployee_ReturnsConflict()
    {
        var repository = new FakeAppointmentRepository();
        var customerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var existing = Appointment.Create(
            Guid.NewGuid(), employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11),
            [new AvailabilityWindow(Day.AddHours(9), Day.AddHours(17))], [], "employee-1", Clock.GetUtcNow()).Appointment!;
        repository.Seed(existing);

        var customerClient = new FakeCustomerVerificationClient();
        customerClient.SeedActive(customerId);
        var employeeClient = new FakeEmployeeAvailabilityClient();
        employeeClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));
        var messageBus = Substitute.For<IMessageBus>();

        var command = new CreateAppointmentCommand(
            customerId, employeeId, Guid.NewGuid(), Day.AddHours(10).AddMinutes(30), Day.AddHours(11).AddMinutes(30), "employee-1");

        var result = await CreateAppointmentHandler.Handle(
            command, repository, customerClient, employeeClient, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CreateAppointmentStatus.Conflict);
        await Assert.That(repository.Appointments).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WithDeactivatedCustomer_RejectsAppointmentCreation()
    {
        var repository = new FakeAppointmentRepository();
        var customerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var customerClient = new FakeCustomerVerificationClient();
        customerClient.SeedActive(customerId, isActive: false);
        var employeeClient = new FakeEmployeeAvailabilityClient();
        employeeClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));
        var messageBus = Substitute.For<IMessageBus>();

        var command = new CreateAppointmentCommand(
            customerId, employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11), "employee-1");

        var result = await CreateAppointmentHandler.Handle(
            command, repository, customerClient, employeeClient, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CreateAppointmentStatus.CustomerInactive);
        await Assert.That(repository.Appointments).IsEmpty();
    }

    [Test]
    public async Task Handle_WithUnknownCustomer_ReturnsCustomerNotFound()
    {
        var repository = new FakeAppointmentRepository();
        var employeeId = Guid.NewGuid();
        var customerClient = new FakeCustomerVerificationClient();
        var employeeClient = new FakeEmployeeAvailabilityClient();
        employeeClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));
        var messageBus = Substitute.For<IMessageBus>();

        var command = new CreateAppointmentCommand(
            Guid.NewGuid(), employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11), "employee-1");

        var result = await CreateAppointmentHandler.Handle(
            command, repository, customerClient, employeeClient, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CreateAppointmentStatus.CustomerNotFound);
    }

    [Test]
    public async Task Handle_WhenCustomerServiceIsUnavailable_RejectsWithDependencyUnavailable()
    {
        var repository = new FakeAppointmentRepository();
        var customerClient = new FakeCustomerVerificationClient { ThrowOnCall = true };
        var employeeClient = new FakeEmployeeAvailabilityClient();
        var messageBus = Substitute.For<IMessageBus>();

        var command = new CreateAppointmentCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11), "employee-1");

        var result = await CreateAppointmentHandler.Handle(
            command, repository, customerClient, employeeClient, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CreateAppointmentStatus.DependencyUnavailable);
        await Assert.That(repository.Appointments).IsEmpty();
    }
}
