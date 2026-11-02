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
    public async Task CreateWithId_WithValidData_ReturnsEmployeeWithExactlyTheGivenId()
    {
        var seedId = Guid.Parse("e573eed0-8c40-749f-f05c-aa6839c81cb4");

        var result = Employee.CreateWithId(
            seedId,
            firstName: "CRM",
            lastName: "Employee",
            email: "employee@crm.local",
            role: EmployeeRole.Employee,
            createdBy: "dev-seed",
            createdAt: Now);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Employee!.Id).IsEqualTo(seedId);
        await Assert.That(result.Employee.Role).IsEqualTo(EmployeeRole.Employee);
    }

    [Test]
    public async Task CreateWithId_WithoutRequiredFields_ReturnsValidationErrorAndNoEmployee()
    {
        var result = Employee.CreateWithId(Guid.NewGuid(), "", "Employee", "employee@crm.local", EmployeeRole.Employee, "dev-seed", Now);

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
    public async Task AddAbsence_ForTermFreeVacationPeriod_IsCreatedWithPendingStatusAndRequester()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;

        var result = employee.AddAbsence(
            AbsenceType.Vacation,
            new DateOnly(2026, 10, 20),
            new DateOnly(2026, 10, 24),
            "employee-1",
            Now);

        await Assert.That(result.Absence!.Status).IsEqualTo(AbsenceStatus.Pending);
        await Assert.That(result.Absence.RequestedBy).IsEqualTo("employee-1");
        await Assert.That(result.Absence.RequestedAt).IsEqualTo(Now);
        await Assert.That(result.Absence.DecidedBy).IsNull();
        await Assert.That(result.Absence.DecidedAt).IsNull();
    }

    [Test]
    public async Task ApproveAbsence_OnPendingRequest_SetsApprovedStatusDeciderAndRaisesAbsenceApprovedEvent()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        var absence = employee.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 24), "employee-1", Now).Absence!;
        var decidedAt = Now.AddHours(2);

        var result = employee.ApproveAbsence(absence.Id, "admin-2", decidedAt);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(absence.Status).IsEqualTo(AbsenceStatus.Approved);
        await Assert.That(absence.DecidedBy).IsEqualTo("admin-2");
        await Assert.That(absence.DecidedAt).IsEqualTo(decidedAt);
        await Assert.That(employee.DomainEvents).Count().IsEqualTo(1);
        var evt = (AbsenceApproved)employee.DomainEvents.Single();
        await Assert.That(evt.EmployeeId).IsEqualTo(employee.Id);
        await Assert.That(evt.AbsenceId).IsEqualTo(absence.Id);
        await Assert.That(evt.RequestedBy).IsEqualTo("employee-1");
        await Assert.That(evt.ApprovedBy).IsEqualTo("admin-2");
    }

    [Test]
    public async Task RejectAbsence_OnPendingRequest_SetsRejectedStatusDeciderAndRaisesAbsenceRejectedEvent()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        var absence = employee.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 24), "employee-1", Now).Absence!;
        var decidedAt = Now.AddHours(2);

        var result = employee.RejectAbsence(absence.Id, "admin-2", decidedAt);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(absence.Status).IsEqualTo(AbsenceStatus.Rejected);
        await Assert.That(absence.DecidedBy).IsEqualTo("admin-2");
        await Assert.That(employee.DomainEvents).Count().IsEqualTo(1);
        var evt = (AbsenceRejected)employee.DomainEvents.Single();
        await Assert.That(evt.AbsenceId).IsEqualTo(absence.Id);
        await Assert.That(evt.RejectedBy).IsEqualTo("admin-2");
    }

    [Test]
    public async Task ApproveAbsence_WithUnknownAbsenceId_ReturnsNotFoundAndRaisesNoEvent()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;

        var result = employee.ApproveAbsence(Guid.NewGuid(), "admin-2", Now);

        await Assert.That(result.Status).IsEqualTo(AbsenceDecisionStatus.NotFound);
        await Assert.That(employee.DomainEvents).IsEmpty();
    }

    [Test]
    public async Task ApproveAbsence_OnAlreadyApprovedRequest_ReturnsConflictAndDoesNotChangeStatus()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        var absence = employee.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 24), "employee-1", Now).Absence!;
        employee.ApproveAbsence(absence.Id, "admin-2", Now.AddHours(1));
        employee.ClearDomainEvents();

        var result = employee.ApproveAbsence(absence.Id, "admin-3", Now.AddHours(2));

        await Assert.That(result.Status).IsEqualTo(AbsenceDecisionStatus.Conflict);
        await Assert.That(absence.Status).IsEqualTo(AbsenceStatus.Approved);
        await Assert.That(absence.DecidedBy).IsEqualTo("admin-2");
        await Assert.That(employee.DomainEvents).IsEmpty();
    }

    [Test]
    public async Task RejectAbsence_OnAlreadyRejectedRequest_ReturnsConflictAndDoesNotChangeStatus()
    {
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        var absence = employee.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 24), "employee-1", Now).Absence!;
        employee.RejectAbsence(absence.Id, "admin-2", Now.AddHours(1));
        employee.ClearDomainEvents();

        var result = employee.RejectAbsence(absence.Id, "admin-3", Now.AddHours(2));

        await Assert.That(result.Status).IsEqualTo(AbsenceDecisionStatus.Conflict);
        await Assert.That(absence.Status).IsEqualTo(AbsenceStatus.Rejected);
        await Assert.That(employee.DomainEvents).IsEmpty();
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
