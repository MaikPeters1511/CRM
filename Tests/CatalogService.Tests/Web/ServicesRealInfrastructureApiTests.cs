using System.Net;
using CatalogService.Tests.Support;

namespace CatalogService.Tests.Web;

/// <summary>
/// Regressionstest fuer Tests/E2E/README.md, Bug 4 (analog customer-service/employee-service):
/// Wolverine-Codegen scheiterte mit einer <c>InvalidServiceLocationException</c> fuer Handler, die
/// transitiv von der scoped <c>DbContextOptions&lt;CatalogDbContext&gt;</c>-Factory abhaengen
/// (<c>IServiceRepository -&gt; EfServiceRepository -&gt; CatalogDbContext</c>), sobald eine echte,
/// migrierte PostgreSQL-Datenbank verwendet wurde. <see cref="ServicesApiTests"/> haette diesen Bug
/// nie gefangen, da dort <c>IServiceRepository</c> durch <see cref="FakeServiceRepository"/> ersetzt
/// wird. Dieser Test verwendet bewusst KEIN Fake, sondern die produktionsechte EF-Core-Infrastruktur
/// gegen einen echten Testcontainers-PostgreSQL.
/// </summary>
[Category("Integration")]
[NotInParallel] // mutiert die Prozess-Umgebungsvariable ConnectionStrings__catalog_db
[ClassDataSource<CatalogDbContainerFixture>(Shared = SharedType.PerTestSession)]
public class ServicesRealInfrastructureApiTests(CatalogDbContainerFixture dbFixture)
{
    [Test]
    public async Task GetServices_AgainstRealMigratedDatabase_ReturnsOkInsteadOfInternalServerError()
    {
        using var factory = new CatalogServiceRealInfrastructureWebFactory(dbFixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/api/services");

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(response.StatusCode)
            .IsEqualTo(HttpStatusCode.OK)
            .Because($"GET /api/services against a real, migrated database must not fail with 500 " +
                     $"(Wolverine InvalidServiceLocationException, Tests/E2E/README.md Bug 4). Body: {body}");
    }
}
