using CustomerService.Domain.Customers;

namespace CustomerService.Tests.Domain;

public class CustomerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Create_WithValidRequiredData_ReturnsPersistableCustomerWithUniqueId()
    {
        var result = Customer.Create(
            firstName: "Max",
            lastName: "Mustermann",
            email: "max@example.com",
            phone: null,
            address: null,
            createdBy: "employee-1",
            createdAt: Now);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Customer).IsNotNull();
        await Assert.That(result.Customer!.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(result.Customer.FirstName).IsEqualTo("Max");
        await Assert.That(result.Customer.LastName).IsEqualTo("Mustermann");
        await Assert.That(result.Customer.IsActive).IsTrue();
        await Assert.That(result.Customer.CreatedAt).IsEqualTo(Now);
        await Assert.That(result.Customer.CreatedBy).IsEqualTo("employee-1");
    }

    [Test]
    [Arguments("", "Mustermann", "max@example.com", null)]
    [Arguments("Max", "", "max@example.com", null)]
    [Arguments("Max", "Mustermann", null, null)]
    [Arguments("Max", "Mustermann", "", "")]
    public async Task Create_WithoutRequiredFields_ReturnsValidationErrorAndNoCustomer(
        string firstName, string lastName, string? email, string? phone)
    {
        var result = Customer.Create(
            firstName,
            lastName,
            email,
            phone,
            address: null,
            createdBy: "employee-1",
            createdAt: Now);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Customer).IsNull();
        await Assert.That(result.Errors).IsNotEmpty();
    }

    [Test]
    public async Task Create_WithOnlyPhoneAndNoEmail_Succeeds()
    {
        var result = Customer.Create(
            "Erika",
            "Musterfrau",
            email: null,
            phone: "0123456789",
            address: null,
            createdBy: "employee-1",
            createdAt: Now);

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Update_WithChangedField_PersistsChangesAndSetsModifiedAudit()
    {
        var customer = Customer.Create(
            "Max", "Mustermann", "max@example.com", null, null, "employee-1", Now).Customer!;

        var modifiedAt = Now.AddDays(1);
        var result = customer.Update(
            "Max",
            "Musterfrau",
            "max@example.com",
            null,
            null,
            modifiedBy: "employee-2",
            modifiedAt: modifiedAt);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(customer.LastName).IsEqualTo("Musterfrau");
        await Assert.That(customer.ModifiedAt).IsEqualTo(modifiedAt);
        await Assert.That(customer.ModifiedBy).IsEqualTo("employee-2");
    }

    [Test]
    public async Task Update_WithoutRequiredFields_ReturnsValidationErrorAndDoesNotChangeState()
    {
        var customer = Customer.Create(
            "Max", "Mustermann", "max@example.com", null, null, "employee-1", Now).Customer!;

        var result = customer.Update(
            "",
            "Musterfrau",
            "max@example.com",
            null,
            null,
            modifiedBy: "employee-2",
            modifiedAt: Now.AddDays(1));

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(customer.FirstName).IsEqualTo("Max");
        await Assert.That(customer.ModifiedAt).IsNull();
    }

    [Test]
    public async Task Deactivate_OnActiveCustomer_SetsInactiveAndRaisesCustomerDeactivatedEvent()
    {
        var customer = Customer.Create(
            "Max", "Mustermann", "max@example.com", null, null, "employee-1", Now).Customer!;

        var deactivatedAt = Now.AddDays(2);
        customer.Deactivate("admin-1", deactivatedAt);

        await Assert.That(customer.IsActive).IsFalse();
        await Assert.That(customer.DomainEvents).Count().IsEqualTo(1);
        var evt = (CustomerDeactivated)customer.DomainEvents.Single();
        await Assert.That(evt.CustomerId).IsEqualTo(customer.Id);
        await Assert.That(evt.DeactivatedBy).IsEqualTo("admin-1");
    }

    [Test]
    public async Task Deactivate_OnAlreadyInactiveCustomer_IsIdempotentAndDoesNotRaiseAnotherEvent()
    {
        var customer = Customer.Create(
            "Max", "Mustermann", "max@example.com", null, null, "employee-1", Now).Customer!;
        customer.Deactivate("admin-1", Now.AddDays(1));
        customer.ClearDomainEvents();

        customer.Deactivate("admin-2", Now.AddDays(2));

        await Assert.That(customer.IsActive).IsFalse();
        await Assert.That(customer.DomainEvents).IsEmpty();
    }
}
