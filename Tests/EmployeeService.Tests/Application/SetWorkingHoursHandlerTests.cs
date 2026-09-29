using EmployeeService.Application.Employees;
using EmployeeService.Application.Employees.SetWorkingHours;
using EmployeeService.Domain.Employees;
using EmployeeService.Tests.Support;

namespace EmployeeService.Tests.Application;

public class SetWorkingHoursHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_WithWeekdayWorkingHours_PersistsSlotsAsAvailableTimeWindow()
    {
        var repository = new FakeEmployeeRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Employee!;
        repository.Seed(employee);

        var slots = new[]
        {
            new WorkingHoursSlotDto(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            new WorkingHoursSlotDto(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            new WorkingHoursSlotDto(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            new WorkingHoursSlotDto(DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            new WorkingHoursSlotDto(DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
        };
        var command = new SetWorkingHoursCommand(employee.Id, slots, "admin-1");

        var result = await SetWorkingHoursHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(SetWorkingHoursStatus.Success);
        await Assert.That(employee.WorkingHours).Count().IsEqualTo(5);
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WithUnknownEmployeeId_ReturnsNotFound()
    {
        var repository = new FakeEmployeeRepository();
        var command = new SetWorkingHoursCommand(
            Guid.NewGuid(),
            [new WorkingHoursSlotDto(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            "admin-1");

        var result = await SetWorkingHoursHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(SetWorkingHoursStatus.NotFound);
    }
}
