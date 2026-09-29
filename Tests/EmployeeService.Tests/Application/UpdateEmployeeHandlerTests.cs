using EmployeeService.Application.Employees;
using EmployeeService.Application.Employees.Update;
using EmployeeService.Domain.Employees;
using EmployeeService.Tests.Support;

namespace EmployeeService.Tests.Application;

public class UpdateEmployeeHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_AssigningService_MakesEmployeeAvailableForThatService()
    {
        var repository = new FakeEmployeeRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        repository.Seed(employee);

        var serviceId = Guid.NewGuid();
        var command = new UpdateEmployeeCommand(
            employee.Id,
            "Max",
            "Mustermann",
            "max@example.com",
            EmployeeRole.Employee,
            [new EmployeeServiceAssignmentDto(serviceId, "Beratung")],
            "admin-1");

        var result = await UpdateEmployeeHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(UpdateEmployeeStatus.Success);
        await Assert.That(employee.Services).Count().IsEqualTo(1);
        await Assert.That(employee.Services[0].ServiceName).IsEqualTo("Beratung");
    }

    [Test]
    public async Task Handle_WithUnknownEmployeeId_ReturnsNotFound()
    {
        var repository = new FakeEmployeeRepository();
        var command = new UpdateEmployeeCommand(Guid.NewGuid(), "Max", "Mustermann", "max@example.com", EmployeeRole.Employee, null, "admin-1");

        var result = await UpdateEmployeeHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(UpdateEmployeeStatus.NotFound);
    }
}
