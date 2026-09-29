using EmployeeService.Domain.Employees;

namespace EmployeeService.Tests.Domain;

public class EmployeeTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Create_WithValidRequiredData_ReturnsPersistableEmployeeWithUniqueId()
    {
        var result = Employee.Create(
            firstName: "Max",
            lastName: "Mustermann",
            email: "max@example.com",
            role: EmployeeRole.Employee,
            createdBy: "admin-1",
            createdAt: Now);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Employee).IsNotNull();
        await Assert.That(result.Employee!.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(result.Employee.FirstName).IsEqualTo("Max");
        await Assert.That(result.Employee.Role).IsEqualTo(EmployeeRole.Employee);
        await Assert.That(result.Employee.IsActive).IsTrue();
        await Assert.That(result.Employee.CreatedAt).IsEqualTo(Now);
        await Assert.That(result.Employee.CreatedBy).IsEqualTo("admin-1");
    }

    [Test]
    [Arguments("", "Mustermann", "max@example.com")]
    [Arguments("Max", "", "max@example.com")]
    [Arguments("Max", "Mustermann", "")]
    [Arguments("Max", "Mustermann", "not-an-email")]
    public async Task Create_WithoutRequiredFields_ReturnsValidationErrorAndNoEmployee(
        string firstName, string lastName, string email)
    {
        var result = Employee.Create(firstName, lastName, email, EmployeeRole.Employee, "admin-1", Now);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Employee).IsNull();
        await Assert.That(result.Errors).IsNotEmpty();
    }

    [Test]
    public async Task AssignService_AddsServiceToEmployee()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        var serviceId = Guid.NewGuid();

        employee.AssignService(serviceId, "Beratung");

        await Assert.That(employee.Services).Count().IsEqualTo(1);
        await Assert.That(employee.Services[0].ServiceName).IsEqualTo("Beratung");
    }

    [Test]
    public async Task AssignService_CalledTwiceForSameService_IsIdempotent()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        var serviceId = Guid.NewGuid();

        employee.AssignService(serviceId, "Beratung");
        employee.AssignService(serviceId, "Beratung");

        await Assert.That(employee.Services).Count().IsEqualTo(1);
    }

    [Test]
    public async Task SetWorkingHours_WithValidWeekdaySlot_PersistsWorkingHours()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;

        var slots = new (DayOfWeek, TimeOnly, TimeOnly)[]
        {
            (DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            (DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            (DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            (DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            (DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
        };

        var result = employee.SetWorkingHours(slots, "admin-1", Now);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(employee.WorkingHours).Count().IsEqualTo(5);
    }

    [Test]
    public async Task SetWorkingHours_WithStartAfterEnd_ReturnsValidationErrorAndDoesNotChangeState()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;

        var slots = new (DayOfWeek, TimeOnly, TimeOnly)[]
        {
            (DayOfWeek.Monday, new TimeOnly(17, 0), new TimeOnly(9, 0)),
        };

        var result = employee.SetWorkingHours(slots, "admin-1", Now);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(employee.WorkingHours).IsEmpty();
    }

    [Test]
    public async Task AddAbsence_ForTermFreeVacationPeriod_PersistsAbsenceAndMarksUnavailable()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;

        var result = employee.AddAbsence(
            AbsenceType.Vacation,
            new DateOnly(2026, 10, 20),
            new DateOnly(2026, 10, 24),
            "employee-1",
            Now);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(employee.Absences).Count().IsEqualTo(1);
        await Assert.That(employee.HasAbsenceOverlap(new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 22))).IsTrue();
    }

    [Test]
    public async Task AddAbsence_WithFromAfterTo_ReturnsValidationError()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;

        var result = employee.AddAbsence(
            AbsenceType.Vacation,
            new DateOnly(2026, 10, 24),
            new DateOnly(2026, 10, 20),
            "employee-1",
            Now);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(employee.Absences).IsEmpty();
    }

    [Test]
    public async Task HasAbsenceOverlap_ForDateOutsideAbsence_ReturnsFalse()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        employee.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 24), "employee-1", Now);

        await Assert.That(employee.HasAbsenceOverlap(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2))).IsFalse();
    }

    [Test]
    public async Task Deactivate_OnActiveEmployee_SetsInactiveAndRaisesEmployeeDeactivatedEvent()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;

        var deactivatedAt = Now.AddDays(2);
        employee.Deactivate("admin-1", deactivatedAt);

        await Assert.That(employee.IsActive).IsFalse();
        await Assert.That(employee.DomainEvents).Count().IsEqualTo(1);
        var evt = (EmployeeDeactivated)employee.DomainEvents.Single();
        await Assert.That(evt.EmployeeId).IsEqualTo(employee.Id);
        await Assert.That(evt.DeactivatedBy).IsEqualTo("admin-1");
    }

    [Test]
    public async Task Deactivate_OnAlreadyInactiveEmployee_IsIdempotentAndDoesNotRaiseAnotherEvent()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        employee.Deactivate("admin-1", Now.AddDays(1));
        employee.ClearDomainEvents();

        employee.Deactivate("admin-2", Now.AddDays(2));

        await Assert.That(employee.IsActive).IsFalse();
        await Assert.That(employee.DomainEvents).IsEmpty();
    }
}
