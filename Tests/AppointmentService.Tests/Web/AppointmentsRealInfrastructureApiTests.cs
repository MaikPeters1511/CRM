using System.Net;
using AppointmentService.Tests.Support;

namespace AppointmentService.Tests.Web;

/// <summary>
/// Regressionstest fuer Tests/E2E/README.md, Bug 4 (appointment-service): Wolverine-Codegen
/// scheiterte mit einer <c>InvalidServiceLocationException</c> fuer Handler, die transitiv von
/// der scoped <c>DbContextOptions&lt;AppointmentDbContext&gt;</c>-Factory abhaengen
/// (<c>IAppointmentRepository -&gt; EfAppointmentRepository -&gt; AppointmentDbContext</c>),
/// sobald eine echte, migrierte PostgreSQL-Datenbank verwendet wurde.
/// <see cref="AppointmentsApiTests"/> haette diesen Bug nie gefangen, da dort
/// <c>IAppointmentRepository</c> durch <see cref="FakeAppointmentRepository"/> ersetzt wird.
/// Dieser Test verwendet bewusst KEIN Fake-Repository, sondern die produktionsechte
/// EF-Core-Infrastruktur gegen einen echten Testcontainers-PostgreSQL.
/// </summary>
[Category("Integration")]
[NotInParallel] // mutiert die Prozess-Umgebungsvariable ConnectionStrings__appointment_db
[ClassDataSource<AppointmentDbContainerFixture>(Shared = SharedType.PerTestSession)]
public class AppointmentsRealInfrastructureApiTests(AppointmentDbContainerFixture dbFixture)
{
    [Test]
    public async Task GetAppointments_AgainstRealMigratedDatabase_ReturnsOkInsteadOfInternalServerError()
    {
        using var factory = new AppointmentServiceRealInfrastructureWebFactory(dbFixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/api/appointments?from=2026-01-01&to=2026-12-31");

        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(response.StatusCode)
            .IsEqualTo(HttpStatusCode.OK)
            .Because($"GET /api/appointments against a real, migrated database must not fail with 500 " +
                     $"(Wolverine InvalidServiceLocationException, Tests/E2E/README.md Bug 4). Body: {body}");
    }
}
