using CustomerService.Application.Customers.Update;
using CustomerService.Domain.Customers;
using CustomerService.Tests.Support;

namespace CustomerService.Tests.Application;

public class UpdateCustomerHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_WithExistingCustomerAndValidChanges_PersistsChangesAndUpdatesAudit()
    {
        var repository = new FakeCustomerRepository();
        var customer = Customer.Create("Max", "Mustermann", "max@example.com", null, null, "employee-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Customer!;
        repository.Seed(customer);

        var command = new UpdateCustomerCommand(customer.Id, "Max", "Musterfrau", "max@example.com", null, null, "employee-2");

        var result = await UpdateCustomerHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(UpdateCustomerStatus.Success);
        await Assert.That(result.Customer!.LastName).IsEqualTo("Musterfrau");
        await Assert.That(result.Customer.ModifiedBy).IsEqualTo("employee-2");
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WithUnknownCustomerId_ReturnsNotFound()
    {
        var repository = new FakeCustomerRepository();
        var command = new UpdateCustomerCommand(Guid.NewGuid(), "Max", "Mustermann", "max@example.com", null, null, "employee-2");

        var result = await UpdateCustomerHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(UpdateCustomerStatus.NotFound);
    }

    [Test]
    public async Task Handle_WithMissingRequiredFields_ReturnsValidationFailedAndDoesNotSave()
    {
        var repository = new FakeCustomerRepository();
        var customer = Customer.Create("Max", "Mustermann", "max@example.com", null, null, "employee-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Customer!;
        repository.Seed(customer);

        var command = new UpdateCustomerCommand(customer.Id, "", "Musterfrau", "max@example.com", null, null, "employee-2");

        var result = await UpdateCustomerHandler.Handle(command, repository, Clock, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(UpdateCustomerStatus.ValidationFailed);
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(0);
    }
}
