using CatalogService.Domain.Services;

namespace CatalogService.Tests.Domain;

public class ServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    // Scenario: Leistung mit gültigem Namen anlegen
    [Test]
    public async Task Create_WithValidName_ReturnsPersistableServiceWithUniqueId()
    {
        var result = Service.Create(
            name: "Beratung",
            description: "Erstberatung",
            defaultDurationMinutes: 30,
            createdBy: "admin-1",
            createdAt: Now);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Service).IsNotNull();
        await Assert.That(result.Service!.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(result.Service.Name).IsEqualTo("Beratung");
        await Assert.That(result.Service.Description).IsEqualTo("Erstberatung");
        await Assert.That(result.Service.DefaultDurationMinutes).IsEqualTo(30);
        await Assert.That(result.Service.IsActive).IsTrue();
        await Assert.That(result.Service.CreatedAt).IsEqualTo(Now);
        await Assert.That(result.Service.CreatedBy).IsEqualTo("admin-1");
    }

    [Test]
    public async Task Create_WithValidName_RaisesServiceCatalogEntryChangedWithCreatedType()
    {
        var result = Service.Create("Beratung", "Erstberatung", 30, "admin-1", Now);

        var domainEvent = result.Service!.DomainEvents.OfType<ServiceCatalogEntryChanged>().Single();
        await Assert.That(domainEvent.ServiceId).IsEqualTo(result.Service.Id);
        await Assert.That(domainEvent.ChangeType).IsEqualTo(ServiceCatalogChangeType.Created);
        await Assert.That(domainEvent.IsActive).IsTrue();
    }

    [Test]
    public async Task Create_WithoutDescriptionAndDuration_Succeeds()
    {
        var result = Service.Create(
            name: "Haarschnitt",
            description: null,
            defaultDurationMinutes: null,
            createdBy: "admin-1",
            createdAt: Now);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Service!.Description).IsNull();
        await Assert.That(result.Service.DefaultDurationMinutes).IsNull();
    }

    // Scenario: Leistung ohne Namen anlegen
    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments((string?)null)]
    public async Task Create_WithoutName_ReturnsValidationErrorAndNoService(string? name)
    {
        var result = Service.Create(
            name!,
            description: null,
            defaultDurationMinutes: null,
            createdBy: "admin-1",
            createdAt: Now);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Service).IsNull();
        await Assert.That(result.Errors).IsNotEmpty();
    }

    // Scenario: Leistung aktualisieren
    [Test]
    public async Task Update_WithChangedField_PersistsChangesAndSetsModifiedAudit()
    {
        var service = Service.Create("Beratung", "Erstberatung", 30, "admin-1", Now).Service!;
        service.ClearDomainEvents();

        var modifiedAt = Now.AddDays(1);
        var result = service.Update(
            "Beratung Premium",
            "Erweiterte Beratung",
            45,
            modifiedBy: "admin-2",
            modifiedAt: modifiedAt);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(service.Name).IsEqualTo("Beratung Premium");
        await Assert.That(service.Description).IsEqualTo("Erweiterte Beratung");
        await Assert.That(service.DefaultDurationMinutes).IsEqualTo(45);
        await Assert.That(service.ModifiedAt).IsEqualTo(modifiedAt);
        await Assert.That(service.ModifiedBy).IsEqualTo("admin-2");

        var domainEvent = service.DomainEvents.OfType<ServiceCatalogEntryChanged>().Single();
        await Assert.That(domainEvent.ChangeType).IsEqualTo(ServiceCatalogChangeType.Updated);
        await Assert.That(domainEvent.Name).IsEqualTo("Beratung Premium");
    }

    [Test]
    public async Task Update_WithoutName_ReturnsValidationErrorAndDoesNotChangeState()
    {
        var service = Service.Create("Beratung", null, null, "admin-1", Now).Service!;

        var result = service.Update("", null, null, modifiedBy: "admin-2", modifiedAt: Now.AddDays(1));

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(service.Name).IsEqualTo("Beratung");
        await Assert.That(service.ModifiedAt).IsNull();
    }

    // Scenario: Leistung deaktivieren
    [Test]
    public async Task Deactivate_OnActiveService_SetsInactiveAndAudit()
    {
        var service = Service.Create("Beratung", null, null, "admin-1", Now).Service!;
        service.ClearDomainEvents();

        var deactivatedAt = Now.AddDays(2);
        service.Deactivate("admin-1", deactivatedAt);

        await Assert.That(service.IsActive).IsFalse();
        await Assert.That(service.ModifiedAt).IsEqualTo(deactivatedAt);
        await Assert.That(service.ModifiedBy).IsEqualTo("admin-1");

        var domainEvent = service.DomainEvents.OfType<ServiceCatalogEntryChanged>().Single();
        await Assert.That(domainEvent.ChangeType).IsEqualTo(ServiceCatalogChangeType.Deactivated);
        await Assert.That(domainEvent.IsActive).IsFalse();
    }

    [Test]
    public async Task Deactivate_OnAlreadyInactiveService_IsIdempotent()
    {
        var service = Service.Create("Beratung", null, null, "admin-1", Now).Service!;
        service.Deactivate("admin-1", Now.AddDays(1));

        service.Deactivate("admin-2", Now.AddDays(2));

        await Assert.That(service.IsActive).IsFalse();
        await Assert.That(service.ModifiedBy).IsEqualTo("admin-1");
    }
}
