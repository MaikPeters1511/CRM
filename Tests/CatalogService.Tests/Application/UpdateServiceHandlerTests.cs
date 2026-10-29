using CatalogService.Application.Services.Update;
using CatalogService.Domain.Services;
using CatalogService.Tests.Support;

namespace CatalogService.Tests.Application;

public class UpdateServiceHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_WithExistingServiceAndValidChanges_PersistsChangesAndUpdatesAudit()
    {
        var repository = new FakeServiceRepository();
        var service = Service.Create("Beratung", null, null, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Service!;
        repository.Seed(service);

        var command = new UpdateServiceCommand(service.Id, "Beratung Premium", "Erweitert", 45, "admin-2");

        var result = await UpdateServiceHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(UpdateServiceStatus.Success);
        await Assert.That(result.Service!.Name).IsEqualTo("Beratung Premium");
        await Assert.That(result.Service.ModifiedBy).IsEqualTo("admin-2");
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WithUnknownServiceId_ReturnsNotFound()
    {
        var repository = new FakeServiceRepository();
        var command = new UpdateServiceCommand(Guid.NewGuid(), "Beratung", null, null, "admin-2");

        var result = await UpdateServiceHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(UpdateServiceStatus.NotFound);
    }

    [Test]
    public async Task Handle_WithoutName_ReturnsValidationFailedAndDoesNotSave()
    {
        var repository = new FakeServiceRepository();
        var service = Service.Create("Beratung", null, null, "admin-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Service!;
        repository.Seed(service);

        var command = new UpdateServiceCommand(service.Id, "", null, null, "admin-2");

        var result = await UpdateServiceHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(UpdateServiceStatus.ValidationFailed);
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(0);
    }
}
