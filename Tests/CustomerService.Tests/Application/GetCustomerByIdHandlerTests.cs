using CustomerService.Application.Customers.GetById;
using CustomerService.Domain.Customers;
using CustomerService.Tests.Support;

namespace CustomerService.Tests.Application;

public class GetCustomerByIdHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_WithExistingCustomer_ReturnsCustomerDetails()
    {
        var repository = new FakeCustomerRepository();
        var customer = Customer.Create("Max", "Mustermann", "max@example.com", null, null, "employee-1", Now).Customer!;
        repository.Seed(customer);

        var result = await GetCustomerByIdHandler.Handle(new GetCustomerByIdQuery(customer.Id), repository, CancellationToken.None);

        await Assert.That(result.Found).IsTrue();
        await Assert.That(result.Customer!.Id).IsEqualTo(customer.Id);
    }

    [Test]
    public async Task Handle_WithUnknownCustomerId_ReturnsNotFound()
    {
        var repository = new FakeCustomerRepository();

        var result = await GetCustomerByIdHandler.Handle(new GetCustomerByIdQuery(Guid.NewGuid()), repository, CancellationToken.None);

        await Assert.That(result.Found).IsFalse();
    }
}
