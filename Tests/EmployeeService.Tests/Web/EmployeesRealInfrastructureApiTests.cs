using System.Net;
using EmployeeService.Tests.Support;

namespace EmployeeService.Tests.Web;

/// <summary>
/// Regressionstest fuer Tests/E2E/README.md, Bug 4: Wolverine-Codegen scheiterte mit einer
/// <c>InvalidServiceLocationException</c> fuer Handler, die transitiv von der scoped
/// <c>DbContextOptions&lt;EmployeeDbContext&gt;</c>-Factory abhaengen (
/// <c>IEmployeeRepository -&gt; EfEmployeeRepository -&gt; EmployeeDbContext</c>), sobald eine echte,
/// migrierte PostgreSQL-Datenbank verwendet wurde. <see cref="EmployeesApiTests"/> (und alle
/// anderen bisherigen Tests) haetten diesen Bug NIE gefangen, weil sie <c>IEmployeeRepository</c>
/// durch <see cref="FakeEmployeeRepository"/> ersetzen und damit genau die DI-Kette umgehen, die
/// den Fehler ausloest. Dieser Test verwendet bewusst KEIN Fake, sondern die produktionsechte
/// EF-Core-Infrastruktur gegen einen echten Testcontainers-PostgreSQL
/// (<see cref="EmployeeDbContainerFixture"/>).
/// </summary>
[Category("Integration")]
[NotInParallel] // mutiert die Prozess-Umgebungsvariable ConnectionStrings__employee_db (siehe EmployeeServiceRealInfrastructureWebFactory)
[ClassDataSource<EmployeeDbContainerFixture>(Shared = SharedType.PerTestSession)]
public class EmployeesRealInfrastructureApiTests(EmployeeDbContainerFixture dbFixture)
{
    [Test]
    public async Task GetEmployees_AgainstRealMigratedDatabase_ReturnsOkInsteadOfInternalServerError()
    {
        using var factory = new EmployeeServiceRealInfrastructureWebFactory(dbFixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/api/employees");

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(response.StatusCode)
            .IsEqualTo(HttpStatusCode.OK)
            .Because($"GET /api/employees against a real, migrated database must not fail with 500 " +
                     $"(Wolverine InvalidServiceLocationException, Tests/E2E/README.md Bug 4). Body: {body}");
    }

    [Test]
    public async Task GetEmployeeById_AgainstRealMigratedDatabase_ReturnsOkInsteadOfInternalServerError()
    {
        using var factory = new EmployeeServiceRealInfrastructureWebFactory(dbFixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        // Von DevEmployeeSeedHostedService (Development-Umgebung, siehe
        // EmployeeServiceRealInfrastructureWebFactory) bekannt und idempotent angelegte Id.
        var seededEmployeeId = Guid.Parse("e573eed0-8c40-749f-f05c-aa6839c81cb4");

        var response = await client.GetAsync($"/api/employees/{seededEmployeeId}");

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(response.StatusCode)
            .IsEqualTo(HttpStatusCode.OK)
            .Because($"GET /api/employees/{{id}} against a real, migrated database must not fail with 500 " +
                     $"(Wolverine InvalidServiceLocationException, Tests/E2E/README.md Bug 4). Body: {body}");
        await Assert.That(body).Contains("employee@crm.local");
    }
}
