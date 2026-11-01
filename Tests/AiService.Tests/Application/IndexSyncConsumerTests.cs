using AiService.Application.IndexSync;
using AiService.Tests.Support;

namespace AiService.Tests.Application;

public class IndexSyncConsumerTests
{
    // ADR 0003, Abschnitt 2: AppointmentCreated haelt den Index aktuell.
    [Test]
    public async Task AppointmentCreatedConsumer_MarksEmployeeBusyForThatWindow()
    {
        var indexClient = new FakeAvailabilityIndexClient();
        var employeeId = Guid.NewGuid();
        var @event = new AppointmentCreatedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), employeeId, Guid.NewGuid(),
            new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));

        await AppointmentIndexSyncConsumers.Handle(@event, indexClient, CancellationToken.None);

        await Assert.That(indexClient.MarkedBusyEmployeeIds).Contains(employeeId);
    }

    [Test]
    public async Task EmployeeDeactivatedConsumer_RemovesEmployeeFromIndex()
    {
        var indexClient = new FakeAvailabilityIndexClient();
        var employeeId = Guid.NewGuid();
        var @event = new EmployeeDeactivatedIntegrationEvent(employeeId, DateTimeOffset.UtcNow, "admin-1");

        await EmployeeDeactivatedConsumer.Handle(@event, indexClient, CancellationToken.None);

        await Assert.That(indexClient.RemovedEmployeeIds).Contains(employeeId);
    }

    [Test]
    public async Task ServiceCatalogChangedConsumer_WithDeactivatedType_RemovesServiceFromIndex()
    {
        var indexClient = new FakeAvailabilityIndexClient();
        var serviceId = Guid.NewGuid();
        var @event = new ServiceCatalogChangedIntegrationEvent(
            serviceId, "Farbe", "Haarfarbe", 60, false, ServiceCatalogChangeType.Deactivated, DateTimeOffset.UtcNow);

        await ServiceCatalogChangedConsumer.Handle(@event, indexClient, CancellationToken.None);

        await Assert.That(indexClient.RemovedServiceIds).Contains(serviceId);
        await Assert.That(indexClient.UpsertedServiceIds).IsEmpty();
    }

    [Test]
    public async Task ServiceCatalogChangedConsumer_WithCreatedType_UpsertsServiceInIndex()
    {
        var indexClient = new FakeAvailabilityIndexClient();
        var serviceId = Guid.NewGuid();
        var @event = new ServiceCatalogChangedIntegrationEvent(
            serviceId, "Farbe", "Haarfarbe", 60, true, ServiceCatalogChangeType.Created, DateTimeOffset.UtcNow);

        await ServiceCatalogChangedConsumer.Handle(@event, indexClient, CancellationToken.None);

        await Assert.That(indexClient.UpsertedServiceIds).Contains(serviceId);
    }
}
