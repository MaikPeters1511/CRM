using EmployeeService.Application.Abstractions;
using EmployeeService.Application.Employees.Availability;
using EmployeeService.Domain.Employees;
using EmployeeService.Tests.Support;

namespace EmployeeService.Tests.Application;

public class GetEmployeeAvailabilityHandlerTests
{
    [Test]
    public async Task Handle_WithWorkingHoursAndNoAppointments_ReturnsFullWorkingWindowAsAvailable()
    {
        var repository = new FakeEmployeeRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        employee.SetWorkingHours(
            [(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        repository.Seed(employee);

        var appointmentClient = new FakeAppointmentAvailabilityClient();

        // 2026-10-19 is a Monday.
        var query = new GetEmployeeAvailabilityQuery(employee.Id, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 19));
        var result = await GetEmployeeAvailabilityHandler.Handle(query, repository, appointmentClient, CancellationToken.None);

        await Assert.That(result.Found).IsTrue();
        await Assert.That(result.Slots).Count().IsEqualTo(1);
        await Assert.That(result.Slots[0].StartTime).IsEqualTo(new TimeOnly(9, 0));
        await Assert.That(result.Slots[0].EndTime).IsEqualTo(new TimeOnly(17, 0));
    }

    [Test]
    public async Task Handle_WithExistingAppointmentInWindow_ExcludesBookedTimeFromAvailability()
    {
        var repository = new FakeEmployeeRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        employee.SetWorkingHours(
            [(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        repository.Seed(employee);

        var appointmentClient = new FakeAppointmentAvailabilityClient();
        appointmentClient.Seed(new ExistingAppointmentSlot(new DateOnly(2026, 10, 19), new TimeOnly(10, 0), new TimeOnly(11, 0)));

        var query = new GetEmployeeAvailabilityQuery(employee.Id, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 19));
        var result = await GetEmployeeAvailabilityHandler.Handle(query, repository, appointmentClient, CancellationToken.None);

        await Assert.That(result.Slots).Count().IsEqualTo(2);
        await Assert.That(result.Slots[0].StartTime).IsEqualTo(new TimeOnly(9, 0));
        await Assert.That(result.Slots[0].EndTime).IsEqualTo(new TimeOnly(10, 0));
        await Assert.That(result.Slots[1].StartTime).IsEqualTo(new TimeOnly(11, 0));
        await Assert.That(result.Slots[1].EndTime).IsEqualTo(new TimeOnly(17, 0));
    }

    [Test]
    public async Task Handle_WithAbsenceCoveringDate_ExcludesThatDayEntirely()
    {
        var repository = new FakeEmployeeRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        employee.SetWorkingHours(
            [(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        employee.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 19), "employee-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        repository.Seed(employee);

        var appointmentClient = new FakeAppointmentAvailabilityClient();

        var query = new GetEmployeeAvailabilityQuery(employee.Id, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 19));
        var result = await GetEmployeeAvailabilityHandler.Handle(query, repository, appointmentClient, CancellationToken.None);

        await Assert.That(result.Slots).IsEmpty();
    }

    [Test]
    public async Task Handle_WithUnknownEmployeeId_ReturnsNotFound()
    {
        var repository = new FakeEmployeeRepository();
        var appointmentClient = new FakeAppointmentAvailabilityClient();

        var query = new GetEmployeeAvailabilityQuery(Guid.NewGuid(), new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 19));
        var result = await GetEmployeeAvailabilityHandler.Handle(query, repository, appointmentClient, CancellationToken.None);

        await Assert.That(result.Found).IsFalse();
    }
}
