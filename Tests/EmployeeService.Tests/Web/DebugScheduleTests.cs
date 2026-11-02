using System.Net;
using System.Net.Http.Json;
using EmployeeService.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EmployeeService.Tests.Web;

/// <summary>
/// Regressionstests fuer Tests/E2E/README.md, Bug 6 und Bug 7 - beide real gegen eine
/// echte, migrierte Testcontainers-PostgreSQL reproduziert (kein Fake-Repository, siehe
/// <see cref="EmployeesRealInfrastructureApiTests"/> fuer den analogen Bug-4-Regressionstest).
/// </summary>
[Category("Integration")]
[NotInParallel] // mutiert die Prozess-Umgebungsvariable ConnectionStrings__employee_db (siehe EmployeeServiceRealInfrastructureWebFactory)
[ClassDataSource<EmployeeDbContainerFixture>(Shared = SharedType.PerTestSession)]
public class ScheduleAndAbsenceRealInfrastructureApiTests(EmployeeDbContainerFixture dbFixture)
{
    /// <summary>
    /// Bug 6: <c>PUT /api/employees/{id}/schedule</c> schlaegt beim allerersten Setzen einer
    /// Arbeitszeit fuer einen Mitarbeiter (bisher leere <c>WorkingHours</c>-Collection) mit
    /// <c>DbUpdateConcurrencyException</c> fehl, weil EF Core fuer die neue, client-seitig per
    /// <c>Guid.NewGuid()</c> erzeugte <c>WorkingHours.Id</c> ein UPDATE statt ein INSERT generiert
    /// (Owned-Type mit eigenem Surrogatschluessel + client-generierter, nicht-leerer Guid ohne
    /// <c>ValueGeneratedNever()</c> - EF interpretiert den bereits gesetzten Schluesselwert als
    /// "Entity existiert schon in der DB"). Siehe <c>EmployeeDbContext.OnModelCreating</c>,
    /// `workingHoursBuilder` fuer <c>Employee.WorkingHours</c>.
    /// </summary>
    [Test]
    public async Task SetWorkingHours_FirstTimeForEmployee_ReturnsOkInsteadOfConcurrencyException()
    {
        using var factory = new EmployeeServiceRealInfrastructureWebFactory(dbFixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var createResponse = await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Debug",
            lastName = "Schedule",
            email = "debug-schedule@crm.local",
            role = "Employee",
        });
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var created = System.Text.Json.JsonDocument.Parse(createBody);
        var id = created.RootElement.GetProperty("id").GetGuid();

        var response = await client.PutAsJsonAsync($"/api/employees/{id}/schedule", new
        {
            slots = new[] { new { weekday = "Monday", startTime = "09:00:00", endTime = "17:00:00" } },
        });
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode)
            .IsEqualTo(HttpStatusCode.OK)
            .Because($"Tests/E2E/README.md Bug 6: erstes Setzen der Arbeitszeit darf nicht mit " +
                     $"DbUpdateConcurrencyException scheitern. Body: {body}");
    }

    /// <summary>
    /// Bug 7: <c>POST /api/employees/{id}/absences</c> schlaegt IMMER (nicht nur beim ersten Mal)
    /// mit einer <c>InvalidServiceLocationException</c> fehl - derselbe Fehlerklasse wie Bug 4,
    /// aber fuer einen ANDEREN, von Bug 4 nicht erfassten Service: <c>IAppointmentAvailabilityClient</c>
    /// (Transient, "opaque lambda factory"). Der Bug-4-Fix hat nur
    /// <c>AlwaysUseServiceLocationFor&lt;IEmployeeRepository&gt;()</c> ergaenzt, nicht diesen
    /// zusaetzlichen Service, der fuer die Abwesenheits-Konfliktpruefung gegen appointment-service
    /// benoetigt wird.
    /// </summary>
    [Test]
    public async Task AddAbsence_ReturnsOkOrConflictInsteadOfInvalidServiceLocationException()
    {
        // EmployeeServiceRealInfrastructureWebFactory registriert bewusst den echten
        // AddHttpClient<IAppointmentAvailabilityClient, ...>-Pfad (kein Fake) - nur so wird der
        // Wolverine-Codegen-Fehler aus Bug 7 ueberhaupt reproduziert/verifiziert. Da in dieser
        // Testumgebung kein echter appointment-service laeuft, stellt ein minimaler lokaler
        // HTTP-Stub sicher, dass der nachgelagerte HTTP-Call (anders als Bug 7 selbst, der VOR
        // jedem Call beim DI-Codegen auftrat) eine echte, erreichbare Gegenstelle findet.
        await using var appointmentServiceStub = WebApplication.CreateBuilder().Build();
        appointmentServiceStub.Urls.Add("http://127.0.0.1:0");
        // abwesenheit-workflow.openspec.md, Abschnitt 5/7: appointment-service's GET /api/appointments
        // liefert seit diesem Change ein Wrapper-Objekt { appointments: [...], absenceEntries: [...] }
        // statt eines nackten Arrays - der Stub muss dieselbe Form liefern, da
        // AppointmentAvailabilityHttpClient jetzt genau dieses Format erwartet.
        appointmentServiceStub.MapGet("/api/appointments", () => Results.Json(new { appointments = Array.Empty<object>() }));
        await appointmentServiceStub.StartAsync();
        var stubBaseUrl = appointmentServiceStub.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        var previousBaseUrlEnvVar = Environment.GetEnvironmentVariable("APPOINTMENT_SERVICE_BASE_URL");
        Environment.SetEnvironmentVariable("APPOINTMENT_SERVICE_BASE_URL", stubBaseUrl);

        try
        {
            using var factory = new EmployeeServiceRealInfrastructureWebFactory(dbFixture.ConnectionString);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

            await RunAddAbsenceAsync(client);
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPOINTMENT_SERVICE_BASE_URL", previousBaseUrlEnvVar);
            await appointmentServiceStub.StopAsync();
        }
    }

    private static async Task RunAddAbsenceAsync(HttpClient client)
    {
        var createResponse = await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Debug",
            lastName = "Absence",
            email = "debug-absence@crm.local",
            role = "Employee",
        });
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var created = System.Text.Json.JsonDocument.Parse(createBody);
        var id = created.RootElement.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync($"/api/employees/{id}/absences", new
        {
            type = "Vacation",
            from = "2027-01-04",
            to = "2027-01-08",
        });
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode)
            .IsNotEqualTo(HttpStatusCode.InternalServerError)
            .Because($"Tests/E2E/README.md Bug 7: Anlegen einer Abwesenheit darf nicht mit " +
                     $"InvalidServiceLocationException (IAppointmentAvailabilityClient) scheitern. Body: {body}");
    }
}
