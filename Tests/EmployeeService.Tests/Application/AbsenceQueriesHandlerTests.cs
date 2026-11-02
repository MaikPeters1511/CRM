using EmployeeService.Application.Employees.Absences;
using EmployeeService.Domain.Employees;
using EmployeeService.Tests.Support;

namespace EmployeeService.Tests.Application;

public class AbsenceQueriesHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task GetEmployeeAbsences_ForEmployeeWithTwoRequestsOfDifferentStatus_ReturnsBothWithStatus()
    {
        var repository = new FakeEmployeeRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        employee.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 24), "employee-1", Now);
        var sick = employee.AddAbsence(AbsenceType.Sick, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2), "employee-1", Now).Absence!;
        employee.ApproveAbsence(sick.Id, "admin-2", Now.AddDays(1));
        repository.Seed(employee);

        var result = await GetEmployeeAbsencesHandler.Handle(new GetEmployeeAbsencesQuery(employee.Id), repository, CancellationToken.None);

        await Assert.That(result.Found).IsTrue();
        await Assert.That(result.Absences).Count().IsEqualTo(2);
        await Assert.That(result.Absences.Count(a => a.Status == AbsenceStatus.Pending)).IsEqualTo(1);
        await Assert.That(result.Absences.Count(a => a.Status == AbsenceStatus.Approved)).IsEqualTo(1);
    }

    [Test]
    public async Task GetEmployeeAbsences_WithUnknownEmployeeId_ReturnsNotFound()
    {
        var repository = new FakeEmployeeRepository();

        var result = await GetEmployeeAbsencesHandler.Handle(new GetEmployeeAbsencesQuery(Guid.NewGuid()), repository, CancellationToken.None);

        await Assert.That(result.Found).IsFalse();
    }

    [Test]
    public async Task GetPendingAbsences_WithPendingAndDecidedRequestsAcrossEmployees_ReturnsOnlyPendingOnes()
    {
        var repository = new FakeEmployeeRepository();
        var employeeA = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        var pending = employeeA.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 24), "employee-1", Now).Absence!;
        var employeeB = Employee.Create("Erika", "Musterfrau", "erika@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        var decided = employeeB.AddAbsence(AbsenceType.Sick, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2), "employee-2", Now).Absence!;
        employeeB.ApproveAbsence(decided.Id, "admin-2", Now.AddDays(1));
        repository.Seed(employeeA);
        repository.Seed(employeeB);

        var result = await GetPendingAbsencesHandler.Handle(new GetPendingAbsencesQuery(), repository, CancellationToken.None);

        await Assert.That(result.Absences).Count().IsEqualTo(1);
        await Assert.That(result.Absences[0].AbsenceId).IsEqualTo(pending.Id);
        await Assert.That(result.Absences[0].EmployeeId).IsEqualTo(employeeA.Id);
    }

    [Test]
    public async Task GetApprovedAbsences_FiltersByStatusAndPeriod_ReturnsOnlyApprovedOverlappingOnes()
    {
        var repository = new FakeEmployeeRepository();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;

        var approvedInRange = employee.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 22), "employee-1", Now).Absence!;
        employee.ApproveAbsence(approvedInRange.Id, "admin-2", Now.AddDays(1));

        var pending = employee.AddAbsence(AbsenceType.Sick, new DateOnly(2026, 11, 21), new DateOnly(2026, 11, 21), "employee-1", Now).Absence!;

        var approvedOutOfRange = employee.AddAbsence(AbsenceType.Vacation, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), "employee-1", Now).Absence!;
        employee.ApproveAbsence(approvedOutOfRange.Id, "admin-2", Now.AddDays(1));

        repository.Seed(employee);

        var result = await GetApprovedAbsencesHandler.Handle(
            new GetApprovedAbsencesQuery(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), null), repository, CancellationToken.None);

        await Assert.That(result.Absences).Count().IsEqualTo(1);
        await Assert.That(result.Absences[0].From).IsEqualTo(approvedInRange.From);
        await Assert.That(pending.Status).IsEqualTo(AbsenceStatus.Pending);
    }
}
