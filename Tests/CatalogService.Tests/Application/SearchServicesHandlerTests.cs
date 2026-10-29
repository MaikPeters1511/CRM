using CatalogService.Application.Services.Search;
using CatalogService.Domain.Services;
using CatalogService.Tests.Support;

namespace CatalogService.Tests.Application;

public class SearchServicesHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Scenario: Suche nach Namen
    [Test]
    public async Task Handle_SearchingByName_ReturnsOnlyMatchingServices()
    {
        var repository = new FakeServiceRepository();
        repository.Seed(Service.Create("Beratung", null, null, "admin-1", Now).Service!);
        repository.Seed(Service.Create("Beratung Premium", null, null, "admin-1", Now).Service!);
        repository.Seed(Service.Create("Haarschnitt", null, null, "admin-1", Now).Service!);

        var result = await SearchServicesHandler.Handle(
            new SearchServicesQuery(Search: "Beratung", IsActive: null),
            repository,
            CancellationToken.None);

        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.Items.All(s => s.Name.Contains("Beratung"))).IsTrue();
    }

    [Test]
    public async Task Handle_FilteringByActiveStatus_ReturnsOnlyInactiveServicesWhenRequested()
    {
        var repository = new FakeServiceRepository();
        var active = Service.Create("Beratung", null, null, "admin-1", Now).Service!;
        var inactive = Service.Create("Haarschnitt", null, null, "admin-1", Now).Service!;
        inactive.Deactivate("admin-1", Now.AddDays(1));
        repository.Seed(active);
        repository.Seed(inactive);

        var result = await SearchServicesHandler.Handle(
            new SearchServicesQuery(Search: null, IsActive: false),
            repository,
            CancellationToken.None);

        await Assert.That(result.Items).Count().IsEqualTo(1);
        await Assert.That(result.Items.Single().IsActive).IsFalse();
    }

    [Test]
    public async Task Handle_WithoutStatusFilter_ReturnsActiveAndInactiveServices()
    {
        var repository = new FakeServiceRepository();
        var active = Service.Create("Beratung", null, null, "admin-1", Now).Service!;
        var inactive = Service.Create("Haarschnitt", null, null, "admin-1", Now).Service!;
        inactive.Deactivate("admin-1", Now.AddDays(1));
        repository.Seed(active);
        repository.Seed(inactive);

        var result = await SearchServicesHandler.Handle(
            new SearchServicesQuery(Search: null, IsActive: null),
            repository,
            CancellationToken.None);

        await Assert.That(result.TotalCount).IsEqualTo(2);
    }
}
