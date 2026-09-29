using System.Net;
using System.Net.Http.Json;
using EmployeeService.Application.Employees.Create;
using EmployeeService.Domain.Employees;

namespace EmployeeService.Tests.Web;

public class EmployeesApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task PostEmployees_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new EmployeeServiceWebFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/employees",
            new CreateEmployeeCommand("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "ignored"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task PostEmployees_AsAdminWithValidData_ReturnsCreatedAndPersistsEmployee()
    {
        using var factory = new EmployeeServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.PostAsJsonAsync("/api/employees",
            new { FirstName = "Max", LastName = "Mustermann", Email = "max@example.com", Role = "Employee" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(factory.Repository.Employees).Count().IsEqualTo(1);
    }

    [Test]
    public async Task PostEmployees_AsEmployee_ReturnsForbidden()
    {
        using var factory = new EmployeeServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.PostAsJsonAsync("/api/employees",
            new { FirstName = "Max", LastName = "Mustermann", Email = "max@example.com", Role = "Employee" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task PostEmployees_WithAlreadyAssignedEmail_ReturnsConflict()
    {
        using var factory = new EmployeeServiceWebFactory();
        factory.Repository.Seed(Employee.Create("Erika", "Musterfrau", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.PostAsJsonAsync("/api/employees",
            new { FirstName = "Max", LastName = "Mustermann", Email = "max@example.com", Role = "Employee" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(factory.Repository.Employees).Count().IsEqualTo(1);
    }

    [Test]
    public async Task PostDeactivate_AsAdmin_DeactivatesEmployee()
    {
        using var factory = new EmployeeServiceWebFactory();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        factory.Repository.Seed(employee);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.PostAsync($"/api/employees/{employee.Id}/deactivate", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(employee.IsActive).IsFalse();
    }

    [Test]
    public async Task PostAbsences_AsEmployeeForOwnRecord_CreatesAbsence()
    {
        using var factory = new EmployeeServiceWebFactory();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        factory.Repository.Seed(employee);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        client.DefaultRequestHeaders.Add("X-Test-UserId", employee.Id.ToString());

        var response = await client.PostAsJsonAsync($"/api/employees/{employee.Id}/absences",
            new { Type = "Vacation", From = "2026-10-19", To = "2026-10-23" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(employee.Absences).Count().IsEqualTo(1);
    }

    [Test]
    public async Task PostAbsences_AsEmployeeForAnotherEmployeeRecord_ReturnsForbidden()
    {
        using var factory = new EmployeeServiceWebFactory();
        var employee = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        factory.Repository.Seed(employee);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        client.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync($"/api/employees/{employee.Id}/absences",
            new { Type = "Vacation", From = "2026-10-19", To = "2026-10-23" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetEmployees_AsEmployeeSearchingByLastName_ReturnsOnlyMatchingEmployees()
    {
        using var factory = new EmployeeServiceWebFactory();
        factory.Repository.Seed(Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!);
        factory.Repository.Seed(Employee.Create("Otto", "Schmidt", "otto@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.GetAsync("/api/employees?search=Muster");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Mustermann");
        await Assert.That(body).DoesNotContain("Schmidt");
    }
}
