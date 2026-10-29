using System.Net;
using System.Net.Http.Json;
using CatalogService.Application.Services.Create;
using CatalogService.Domain.Services;

namespace CatalogService.Tests.Web;

public class ServicesApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task PostServices_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new CatalogServiceWebFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/services",
            new CreateServiceCommand("Beratung", null, null, "ignored"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // Scenario: Leistung mit gültigem Namen anlegen
    [Test]
    public async Task PostServices_AsAdminWithValidName_ReturnsCreatedAndPersistsService()
    {
        using var factory = new CatalogServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.PostAsJsonAsync("/api/services",
            new { Name = "Beratung", Description = "Erstberatung", DefaultDurationMinutes = 30 });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(factory.Repository.Services).Count().IsEqualTo(1);
    }

    // Scenario: Leistung ohne Namen anlegen
    [Test]
    public async Task PostServices_AsAdminWithoutName_ReturnsValidationProblem()
    {
        using var factory = new CatalogServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.PostAsJsonAsync("/api/services",
            new { Name = "", Description = (string?)null, DefaultDurationMinutes = (int?)null });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(factory.Repository.Services).IsEmpty();
    }

    [Test]
    public async Task PostServices_AsEmployee_ReturnsForbidden()
    {
        using var factory = new CatalogServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.PostAsJsonAsync("/api/services",
            new { Name = "Beratung", Description = (string?)null, DefaultDurationMinutes = (int?)null });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    // Scenario: Leistung aktualisieren
    [Test]
    public async Task PutServices_AsAdminWithChangedField_PersistsChanges()
    {
        using var factory = new CatalogServiceWebFactory();
        var service = Service.Create("Beratung", null, null, "admin-1", Now).Service!;
        factory.Repository.Seed(service);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.PutAsJsonAsync($"/api/services/{service.Id}",
            new { Name = "Beratung Premium", Description = (string?)null, DefaultDurationMinutes = (int?)null });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(service.Name).IsEqualTo("Beratung Premium");
    }

    [Test]
    public async Task PutServices_AsEmployee_ReturnsForbidden()
    {
        using var factory = new CatalogServiceWebFactory();
        var service = Service.Create("Beratung", null, null, "admin-1", Now).Service!;
        factory.Repository.Seed(service);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.PutAsJsonAsync($"/api/services/{service.Id}",
            new { Name = "Beratung Premium", Description = (string?)null, DefaultDurationMinutes = (int?)null });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    // Scenario: Leistung deaktivieren
    [Test]
    public async Task PostDeactivate_AsEmployee_ReturnsForbidden()
    {
        using var factory = new CatalogServiceWebFactory();
        var service = Service.Create("Beratung", null, null, "admin-1", Now).Service!;
        factory.Repository.Seed(service);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.PostAsync($"/api/services/{service.Id}/deactivate", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task PostDeactivate_AsAdmin_DeactivatesService()
    {
        using var factory = new CatalogServiceWebFactory();
        var service = Service.Create("Beratung", null, null, "admin-1", Now).Service!;
        factory.Repository.Seed(service);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.PostAsync($"/api/services/{service.Id}/deactivate", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(service.IsActive).IsFalse();
    }

    // Scenario: deaktivierte Leistung erscheint nicht mehr im Standardfilter
    [Test]
    public async Task GetServices_AfterDeactivation_DoesNotAppearInDefaultActiveFilter()
    {
        using var factory = new CatalogServiceWebFactory();
        var service = Service.Create("Beratung", null, null, "admin-1", Now).Service!;
        service.Deactivate("admin-1", Now.AddDays(1));
        factory.Repository.Seed(service);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.GetAsync("/api/services?status=true");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).DoesNotContain("Beratung");
    }

    // Scenario: Suche nach Namen
    [Test]
    public async Task GetServices_AsEmployeeSearchingByName_ReturnsOnlyMatchingServices()
    {
        using var factory = new CatalogServiceWebFactory();
        factory.Repository.Seed(Service.Create("Beratung", null, null, "admin-1", Now).Service!);
        factory.Repository.Seed(Service.Create("Haarschnitt", null, null, "admin-1", Now).Service!);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.GetAsync("/api/services?search=Beratung");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Beratung");
        await Assert.That(body).DoesNotContain("Haarschnitt");
    }
}
