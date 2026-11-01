using CatalogService.Application.IntegrationEvents;
using CatalogService.Application.Services.Update;
using CatalogService.Domain.Services;
using CatalogService.Tests.Support;
using NSubstitute;
using Wolverine;

namespace CatalogService.Tests.Application;

public class UpdateServiceHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_WithExistingServiceAndValidChanges_PersistsChangesAndUpdatesAudit()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var service = Service.Create("Beratung", null, null, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Service!;
        repository.Seed(service);

        var command = new UpdateServiceCommand(service.Id, "Beratung Premium", "Erweitert", 45, "admin-2");

        var result = await UpdateServiceHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(UpdateServiceStatus.Success);
        await Assert.That(result.Service!.Name).IsEqualTo("Beratung Premium");
        await Assert.That(result.Service.ModifiedBy).IsEqualTo("admin-2");
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WithExistingServiceAndValidChanges_PublishesServiceCatalogChangedIntegrationEvent()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var service = Service.Create("Beratung", null, null, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Service!;
        repository.Seed(service);

        var command = new UpdateServiceCommand(service.Id, "Beratung Premium", "Erweitert", 45, "admin-2");

        await UpdateServiceHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await messageBus.Received(1).PublishAsync(
            Arg.Is<ServiceCatalogChangedIntegrationEvent>(e =>
                e.ServiceId == service.Id &&
                e.Name == "Beratung Premium" &&
                e.ChangeType == ServiceCatalogChangeType.Updated));
    }

    [Test]
    public async Task Handle_WithUnknownServiceId_ReturnsNotFound()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var command = new UpdateServiceCommand(Guid.NewGuid(), "Beratung", null, null, "admin-2");

        var result = await UpdateServiceHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(UpdateServiceStatus.NotFound);
    }

    [Test]
    public async Task Handle_WithoutName_ReturnsValidationFailedAndDoesNotSave()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var service = Service.Create("Beratung", null, null, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Service!;
        repository.Seed(service);

        var command = new UpdateServiceCommand(service.Id, "", null, null, "admin-2");

        var result = await UpdateServiceHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(UpdateServiceStatus.ValidationFailed);
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(0);
        await messageBus.DidNotReceive().PublishAsync(Arg.Any<ServiceCatalogChangedIntegrationEvent>());
    }
}
