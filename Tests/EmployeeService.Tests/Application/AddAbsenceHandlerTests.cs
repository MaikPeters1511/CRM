using EmployeeService.Application.Abstractions;
using EmployeeService.Application.Employees.AddAbsence;
using EmployeeService.Domain.Employees;
using EmployeeService.Tests.Support;

namespace EmployeeService.Tests.Application;

public class AddAbsenceHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_ForTermFreePeriod_PersistsAbsenceAndMarksEmployeeUnavailable()
    {
        var repository = new FakeEmployeeRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        repository.Seed(employee);
        var appointmentClient = new FakeAppointmentAvailabilityClient();

        var command = new AddAbsenceCommand(
            employee.Id, AbsenceType.Vacation, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 23), "employee-1");

        var result = await AddAbsenceHandler.Handle(command, repository, appointmentClient, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(AddAbsenceStatus.Success);
        await Assert.That(employee.Absences).Count().IsEqualTo(1);
        await Assert.That(employee.HasAbsenceOverlap(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 20))).IsTrue();
    }

    [Test]
    public async Task Handle_WhenExistingAppointmentFallsInPeriod_ReturnsConflictAndDoesNotPersistAbsence()
    {
        var repository = new FakeEmployeeRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        repository.Seed(employee);

        var appointmentClient = new FakeAppointmentAvailabilityClient();
        appointmentClient.Seed(new ExistingAppointmentSlot(new DateOnly(2026, 10, 15), new TimeOnly(10, 0), new TimeOnly(11, 0)));

        var command = new AddAbsenceCommand(
            employee.Id, AbsenceType.Vacation, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 15), "employee-1");

        var result = await AddAbsenceHandler.Handle(command, repository, appointmentClient, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(AddAbsenceStatus.Conflict);
        await Assert.That(employee.Absences).IsEmpty();
        await Assert.That(result.ConflictingAppointments).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WhenConflictConfirmedExplicitly_PersistsAbsenceDespiteExistingAppointment()
    {
        var repository = new FakeEmployeeRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        repository.Seed(employee);

        var appointmentClient = new FakeAppointmentAvailabilityClient();
        appointmentClient.Seed(new ExistingAppointmentSlot(new DateOnly(2026, 10, 15), new TimeOnly(10, 0), new TimeOnly(11, 0)));

        var command = new AddAbsenceCommand(
            employee.Id, AbsenceType.Vacation, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 15), "employee-1", ConfirmOverride: true);

        var result = await AddAbsenceHandler.Handle(command, repository, appointmentClient, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(AddAbsenceStatus.Success);
        await Assert.That(employee.Absences).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WithUnknownEmployeeId_ReturnsNotFound()
    {
        var repository = new FakeEmployeeRepository();
        var appointmentClient = new FakeAppointmentAvailabilityClient();
        var command = new AddAbsenceCommand(Guid.NewGuid(), AbsenceType.Sick, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 15), "employee-1");

        var result = await AddAbsenceHandler.Handle(command, repository, appointmentClient, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(AddAbsenceStatus.NotFound);
    }
}
