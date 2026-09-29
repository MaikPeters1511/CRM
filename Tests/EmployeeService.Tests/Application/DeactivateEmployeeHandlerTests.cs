using EmployeeService.Application.Employees.Deactivate;
using EmployeeService.Domain.Employees;
using EmployeeService.Tests.Support;
using NSubstitute;
using Wolverine;

namespace EmployeeService.Tests.Application;

public class DeactivateEmployeeHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 3, 12, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_OnActiveEmployee_MarksInactiveAndPublishesEmployeeDeactivatedIntegrationEvent()
    {
        var repository = new FakeEmployeeRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        repository.Seed(employee);

        var messageBus = Substitute.For<IMessageBus>();
        var command = new DeactivateEmployeeCommand(employee.Id, "admin-1");

        var result = await DeactivateEmployeeHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(DeactivateEmployeeStatus.Success);
        await Assert.That(employee.IsActive).IsFalse();
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
        await messageBus.Received(1).PublishAsync(
            Arg.Is<EmployeeDeactivatedIntegrationEvent>(e => e.EmployeeId == employee.Id && e.DeactivatedBy == "admin-1"));
    }

    [Test]
    public async Task Handle_WithUnknownEmployeeId_ReturnsNotFoundAndPublishesNoEvent()
    {
        var repository = new FakeEmployeeRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var command = new DeactivateEmployeeCommand(Guid.NewGuid(), "admin-1");

        var result = await DeactivateEmployeeHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(DeactivateEmployeeStatus.NotFound);
        await messageBus.DidNotReceive().PublishAsync(Arg.Any<EmployeeDeactivatedIntegrationEvent>());
    }
}
