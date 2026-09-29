using EmployeeService.Application.Employees.Create;
using EmployeeService.Domain.Employees;
using EmployeeService.Tests.Support;

namespace EmployeeService.Tests.Application;

public class CreateEmployeeHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_WithValidRequiredData_PersistsEmployeeAndReturnsSuccess()
    {
        var repository = new FakeEmployeeRepository();
        var command = new CreateEmployeeCommand("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1");

        var result = await CreateEmployeeHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CreateEmployeeStatus.Success);
        await Assert.That(result.Employee).IsNotNull();
        await Assert.That(repository.Employees).Count().IsEqualTo(1);
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WithEmailAlreadyAssignedToExistingEmployee_ReturnsEmailConflictAndPersistsNothing()
    {
        var repository = new FakeEmployeeRepository();
        repository.Seed(Employee.Create("Erika", "Musterfrau", "max@example.com", EmployeeRole.Employee, "admin-1", Clock.GetUtcNow()).Employee!);

        var command = new CreateEmployeeCommand("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1");

        var result = await CreateEmployeeHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CreateEmployeeStatus.EmailConflict);
        await Assert.That(repository.Employees).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WithoutRequiredFields_ReturnsValidationErrorAndPersistsNothing()
    {
        var repository = new FakeEmployeeRepository();
        var command = new CreateEmployeeCommand("", "", "", EmployeeRole.Employee, "admin-1");

        var result = await CreateEmployeeHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(CreateEmployeeStatus.ValidationFailed);
        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(repository.Employees).IsEmpty();
    }
}
