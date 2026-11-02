using EmployeeService.Application.Employees.ApproveAbsence;
using EmployeeService.Domain.Employees;
using EmployeeService.Tests.Support;
using NSubstitute;
using Wolverine;

namespace EmployeeService.Tests.Application;

public class ApproveAbsenceHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_OnPendingRequest_ApprovesCreatesNotificationAndPublishesIntegrationEvent()
    {
        var employeeRepository = new FakeEmployeeRepository();
        var notificationRepository = new FakeNotificationRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        var absence = employee.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 24), "employee-1", Clock.GetUtcNow()).Absence!;
        employeeRepository.Seed(employee);

        var messageBus = Substitute.For<IMessageBus>();
        var command = new ApproveAbsenceCommand(employee.Id, absence.Id, "admin-2");

        var result = await ApproveAbsenceHandler.Handle(command, employeeRepository, notificationRepository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(ApproveAbsenceStatus.Success);
        await Assert.That(result.Absence!.Status).IsEqualTo(AbsenceStatus.Approved);
        await Assert.That(employeeRepository.SaveChangesCallCount).IsEqualTo(1);
        await Assert.That(notificationRepository.Notifications).Count().IsEqualTo(1);
        await Assert.That(notificationRepository.Notifications[0].RecipientEmployeeId).IsEqualTo(employee.Id);
        await Assert.That(notificationRepository.Notifications[0].IsRead).IsFalse();
        await messageBus.Received(1).PublishAsync(
            Arg.Is<AbsenceApprovedIntegrationEvent>(e => e.AbsenceId == absence.Id && e.EmployeeId == employee.Id && e.ApprovedBy == "admin-2"));
    }

    [Test]
    public async Task Handle_OnAlreadyDecidedRequest_ReturnsConflictAndPublishesNoEvent()
    {
        var employeeRepository = new FakeEmployeeRepository();
        var notificationRepository = new FakeNotificationRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        var absence = employee.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 24), "employee-1", Clock.GetUtcNow()).Absence!;
        employee.ApproveAbsence(absence.Id, "admin-2", Clock.GetUtcNow());
        employee.ClearDomainEvents();
        employeeRepository.Seed(employee);

        var messageBus = Substitute.For<IMessageBus>();
        var command = new ApproveAbsenceCommand(employee.Id, absence.Id, "admin-3");

        var result = await ApproveAbsenceHandler.Handle(command, employeeRepository, notificationRepository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(ApproveAbsenceStatus.Conflict);
        await Assert.That(notificationRepository.Notifications).IsEmpty();
        await messageBus.DidNotReceive().PublishAsync(Arg.Any<AbsenceApprovedIntegrationEvent>());
    }

    [Test]
    public async Task Handle_WithUnknownEmployeeId_ReturnsNotFound()
    {
        var employeeRepository = new FakeEmployeeRepository();
        var notificationRepository = new FakeNotificationRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var command = new ApproveAbsenceCommand(Guid.NewGuid(), Guid.NewGuid(), "admin-2");

        var result = await ApproveAbsenceHandler.Handle(command, employeeRepository, notificationRepository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(ApproveAbsenceStatus.NotFound);
    }

    [Test]
    public async Task Handle_WithUnknownAbsenceId_ReturnsNotFound()
    {
        var employeeRepository = new FakeEmployeeRepository();
        var notificationRepository = new FakeNotificationRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        employeeRepository.Seed(employee);

        var messageBus = Substitute.For<IMessageBus>();
        var command = new ApproveAbsenceCommand(employee.Id, Guid.NewGuid(), "admin-2");

        var result = await ApproveAbsenceHandler.Handle(command, employeeRepository, notificationRepository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(ApproveAbsenceStatus.NotFound);
    }
}
