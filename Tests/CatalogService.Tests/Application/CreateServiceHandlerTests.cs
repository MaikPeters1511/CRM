using CatalogService.Application.IntegrationEvents;
using CatalogService.Application.Services.Create;
using CatalogService.Domain.Services;
using CatalogService.Tests.Support;
using NSubstitute;
using Wolverine;

namespace CatalogService.Tests.Application;

public class CreateServiceHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_WithValidName_PersistsServiceAndReturnsSuccess()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var command = new CreateServiceCommand("Beratung", "Erstberatung", 30, "admin-1");

        var result = await CreateServiceHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Service).IsNotNull();
        await Assert.That(repository.Services).Count().IsEqualTo(1);
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
    }

    // Folgeaufgabe aus docs/adr/0003-ai-service-qdrant-index-strategie.md: ai-service konsumiert
    // dieses Event, um seinen Qdrant-Verfuegbarkeitsindex aktuell zu halten.
    [Test]
    public async Task Handle_WithValidName_PublishesServiceCatalogChangedIntegrationEvent()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var command = new CreateServiceCommand("Beratung", "Erstberatung", 30, "admin-1");

        var result = await CreateServiceHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await messageBus.Received(1).PublishAsync(
            Arg.Is<ServiceCatalogChangedIntegrationEvent>(e =>
                e.ServiceId == result.Service!.Id &&
                e.Name == "Beratung" &&
                e.IsActive &&
                e.ChangeType == ServiceCatalogChangeType.Created));
    }

    [Test]
    public async Task Handle_WithoutName_ReturnsValidationErrorAndPersistsNothing()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var command = new CreateServiceCommand("", null, null, "admin-1");

        var result = await CreateServiceHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(repository.Services).IsEmpty();
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(0);
        await messageBus.DidNotReceive().PublishAsync(Arg.Any<ServiceCatalogChangedIntegrationEvent>());
    }

    [Test]
    public async Task Handle_WithoutDescriptionAndDuration_PersistsServiceWithoutThem()
    {
        var repository = new FakeServiceRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var command = new CreateServiceCommand("Haarschnitt", null, null, "admin-1");

        var result = await CreateServiceHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Service!.Description).IsNull();
        await Assert.That(result.Service.DefaultDurationMinutes).IsNull();
    }
}
