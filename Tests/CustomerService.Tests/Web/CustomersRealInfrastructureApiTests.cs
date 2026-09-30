using System.Net;
using CustomerService.Tests.Support;

namespace CustomerService.Tests.Web;

/// <summary>
/// Regressionstest fuer Tests/E2E/README.md, Bug 4 (customer-service): Wolverine-Codegen
/// scheiterte mit einer <c>InvalidServiceLocationException</c> fuer Handler, die transitiv von
/// der scoped <c>DbContextOptions&lt;CustomerDbContext&gt;</c>-Factory abhaengen
/// (<c>ICustomerRepository -&gt; EfCustomerRepository -&gt; CustomerDbContext</c>), sobald eine
/// echte, migrierte PostgreSQL-Datenbank verwendet wurde. <see cref="CustomersApiTests"/> haette
/// diesen Bug nie gefangen, da dort <c>ICustomerRepository</c> durch
/// <see cref="FakeCustomerRepository"/> ersetzt wird. Dieser Test verwendet bewusst KEIN Fake,
/// sondern die produktionsechte EF-Core-Infrastruktur gegen einen echten Testcontainers-PostgreSQL.
/// </summary>
[Category("Integration")]
[NotInParallel] // mutiert die Prozess-Umgebungsvariable ConnectionStrings__customer_db
[ClassDataSource<CustomerDbContainerFixture>(Shared = SharedType.PerTestSession)]
public class CustomersRealInfrastructureApiTests(CustomerDbContainerFixture dbFixture)
{
    [Test]
    public async Task GetCustomers_AgainstRealMigratedDatabase_ReturnsOkInsteadOfInternalServerError()
    {
        using var factory = new CustomerServiceRealInfrastructureWebFactory(dbFixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/api/customers");

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(response.StatusCode)
            .IsEqualTo(HttpStatusCode.OK)
            .Because($"GET /api/customers against a real, migrated database must not fail with 500 " +
                     $"(Wolverine InvalidServiceLocationException, Tests/E2E/README.md Bug 4). Body: {body}");
    }
}
