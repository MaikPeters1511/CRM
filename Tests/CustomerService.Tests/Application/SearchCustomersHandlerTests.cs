using CustomerService.Application.Customers.Search;
using CustomerService.Domain.Customers;
using CustomerService.Tests.Support;

namespace CustomerService.Tests.Application;

public class SearchCustomersHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_SearchingByLastName_ReturnsOnlyMatchingCustomers()
    {
        var repository = new FakeCustomerRepository();
        repository.Seed(Customer.Create("Max", "Mustermann", "max@example.com", null, null, "employee-1", Now).Customer!);
        repository.Seed(Customer.Create("Erika", "Musterfrau", "erika@example.com", null, null, "employee-1", Now).Customer!);
        repository.Seed(Customer.Create("Otto", "Schmidt", "otto@example.com", null, null, "employee-1", Now).Customer!);

        var result = await SearchCustomersHandler.Handle(
            new SearchCustomersQuery(Search: "Muster", IsActive: null),
            repository,
            CancellationToken.None);

        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.Items.All(c => c.LastName.Contains("Muster"))).IsTrue();
    }

    [Test]
    public async Task Handle_FilteringByActiveStatus_ReturnsOnlyInactiveCustomersWhenRequested()
    {
        var repository = new FakeCustomerRepository();
        var active = Customer.Create("Max", "Mustermann", "max@example.com", null, null, "employee-1", Now).Customer!;
        var inactive = Customer.Create("Erika", "Musterfrau", "erika@example.com", null, null, "employee-1", Now).Customer!;
        inactive.Deactivate("admin-1", Now.AddDays(1));
        repository.Seed(active);
        repository.Seed(inactive);

        var result = await SearchCustomersHandler.Handle(
            new SearchCustomersQuery(Search: null, IsActive: false),
            repository,
            CancellationToken.None);

        await Assert.That(result.Items).Count().IsEqualTo(1);
        await Assert.That(result.Items.Single().IsActive).IsFalse();
    }

    [Test]
    public async Task Handle_WithoutStatusFilter_ReturnsActiveAndInactiveCustomers()
    {
        var repository = new FakeCustomerRepository();
        var active = Customer.Create("Max", "Mustermann", "max@example.com", null, null, "employee-1", Now).Customer!;
        var inactive = Customer.Create("Erika", "Musterfrau", "erika@example.com", null, null, "employee-1", Now).Customer!;
        inactive.Deactivate("admin-1", Now.AddDays(1));
        repository.Seed(active);
        repository.Seed(inactive);

        var result = await SearchCustomersHandler.Handle(
            new SearchCustomersQuery(Search: null, IsActive: null),
            repository,
            CancellationToken.None);

        await Assert.That(result.TotalCount).IsEqualTo(2);
    }
}
