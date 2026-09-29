using CustomerService.Application.Customers;
using CustomerService.Application.Customers.Create;
using CustomerService.Tests.Support;

namespace CustomerService.Tests.Application;

public class CreateCustomerHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_WithValidRequiredData_PersistsCustomerAndReturnsSuccess()
    {
        var repository = new FakeCustomerRepository();
        var command = new CreateCustomerCommand("Max", "Mustermann", "max@example.com", null, null, "employee-1");

        var result = await CreateCustomerHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Customer).IsNotNull();
        await Assert.That(repository.Customers).Count().IsEqualTo(1);
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WithoutFirstNameAndLastName_ReturnsValidationErrorAndPersistsNothing()
    {
        var repository = new FakeCustomerRepository();
        var command = new CreateCustomerCommand("", "", "max@example.com", null, null, "employee-1");

        var result = await CreateCustomerHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(repository.Customers).IsEmpty();
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_WithAddress_PersistsCustomerWithAddress()
    {
        var repository = new FakeCustomerRepository();
        var address = new AddressDto("Hauptstr. 1", "Berlin", "10115", "DE");
        var command = new CreateCustomerCommand("Max", "Mustermann", "max@example.com", null, address, "employee-1");

        var result = await CreateCustomerHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Customer!.Address).IsNotNull();
        await Assert.That(result.Customer.Address!.City).IsEqualTo("Berlin");
    }
}
