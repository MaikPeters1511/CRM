using CatalogService.Application.IntegrationEvents;
using CatalogService.Application.Services.Deactivate;
using CatalogService.Domain.Services;
using CatalogService.Tests.Support;
using NSubstitute;
using Wolverine;

namespace CatalogService.Tests.Application;

public class DeactivateServiceHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 3, 12, 0, 0, TimeSpan.Zero));

    // Scenario: Leistung deaktivieren
    [Test]
    public async Task Handle_OnActiveService_MarksInactive()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var service = Service.Create("Beratung", null, null, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Service!;
        repository.Seed(service);

        var command = new DeactivateServiceCommand(service.Id, "admin-1");

        var result = await DeactivateServiceHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(DeactivateServiceStatus.Success);
        await Assert.That(service.IsActive).IsFalse();
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_OnActiveService_PublishesServiceCatalogChangedIntegrationEventWithDeactivatedType()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var service = Service.Create("Beratung", null, null, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Service!;
        repository.Seed(service);

        var command = new DeactivateServiceCommand(service.Id, "admin-1");

        await DeactivateServiceHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await messageBus.Received(1).PublishAsync(
            Arg.Is<ServiceCatalogChangedIntegrationEvent>(e =>
                e.ServiceId == service.Id &&
                !e.IsActive &&
                e.ChangeType == ServiceCatalogChangeType.Deactivated));
    }

    [Test]
    public async Task Handle_WithUnknownServiceId_ReturnsNotFound()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var command = new DeactivateServiceCommand(Guid.NewGuid(), "admin-1");

        var result = await DeactivateServiceHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(DeactivateServiceStatus.NotFound);
    }

    // Scenario: Leistung deaktivieren -> erscheint nicht mehr im Standardfilter, bleibt ueber "Inaktiv" sichtbar
    [Test]
    public async Task Handle_AfterDeactivation_ServiceIsExcludedFromActiveFilterButVisibleWhenInactiveRequested()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var service = Service.Create("Beratung", null, null, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Service!;
        repository.Seed(service);

        await DeactivateServiceHandler.Handle(new DeactivateServiceCommand(service.Id, "admin-1"), repository, Clock, messageBus, CancellationToken.None);

        var activeOnly = await repository.SearchAsync(null, true, 1, 20, CancellationToken.None);
        var inactiveOnly = await repository.SearchAsync(null, false, 1, 20, CancellationToken.None);

        await Assert.That(activeOnly.Items).IsEmpty();
        await Assert.That(inactiveOnly.Items).Count().IsEqualTo(1);
    }
}
