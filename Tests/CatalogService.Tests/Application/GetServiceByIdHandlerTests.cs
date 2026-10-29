using CatalogService.Application.Services.GetById;
using CatalogService.Domain.Services;
using CatalogService.Tests.Support;

namespace CatalogService.Tests.Application;

public class GetServiceByIdHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_WithExistingService_ReturnsServiceDetails()
    {
        var repository = new FakeServiceRepository();
        var service = Service.Create("Beratung", null, null, "admin-1", Now).Service!;
        repository.Seed(service);

        var result = await GetServiceByIdHandler.Handle(new GetServiceByIdQuery(service.Id), repository, CancellationToken.None);

        await Assert.That(result.Found).IsTrue();
        await Assert.That(result.Service!.Id).IsEqualTo(service.Id);
    }

    [Test]
    public async Task Handle_WithUnknownServiceId_ReturnsNotFound()
    {
        var repository = new FakeServiceRepository();

        var result = await GetServiceByIdHandler.Handle(new GetServiceByIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        await Assert.That(result.Found).IsFalse();
    }
}
