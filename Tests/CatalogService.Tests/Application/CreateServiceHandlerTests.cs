using CatalogService.Application.Services.Create;
using CatalogService.Tests.Support;

namespace CatalogService.Tests.Application;

public class CreateServiceHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_WithValidName_PersistsServiceAndReturnsSuccess()
    {
        var repository = new FakeServiceRepository();
        var command = new CreateServiceCommand("Beratung", "Erstberatung", 30, "admin-1");

        var result = await CreateServiceHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Service).IsNotNull();
        await Assert.That(repository.Services).Count().IsEqualTo(1);
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WithoutName_ReturnsValidationErrorAndPersistsNothing()
    {
        var repository = new FakeServiceRepository();
        var command = new CreateServiceCommand("", null, null, "admin-1");

        var result = await CreateServiceHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(repository.Services).IsEmpty();
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_WithoutDescriptionAndDuration_PersistsServiceWithoutThem()
    {
        var repository = new FakeServiceRepository();
        var command = new CreateServiceCommand("Haarschnitt", null, null, "admin-1");

        var result = await CreateServiceHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Service!.Description).IsNull();
        await Assert.That(result.Service.DefaultDurationMinutes).IsNull();
    }
}
