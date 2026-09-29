using CustomerService.Application.Customers.Deactivate;
using CustomerService.Domain.Customers;
using CustomerService.Tests.Support;
using NSubstitute;
using Wolverine;

namespace CustomerService.Tests.Application;

public class DeactivateCustomerHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 3, 12, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task Handle_OnActiveCustomer_MarksInactiveAndPublishesCustomerDeactivatedIntegrationEvent()
    {
        var repository = new FakeCustomerRepository();
        var customer = Customer.Create("Max", "Mustermann", "max@example.com", null, null, "employee-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Customer!;
        repository.Seed(customer);

        var messageBus = Substitute.For<IMessageBus>();
        var command = new DeactivateCustomerCommand(customer.Id, "admin-1");

        var result = await DeactivateCustomerHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(DeactivateCustomerStatus.Success);
        await Assert.That(customer.IsActive).IsFalse();
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
        await messageBus.Received(1).PublishAsync(
            Arg.Is<CustomerDeactivatedIntegrationEvent>(e => e.CustomerId == customer.Id && e.DeactivatedBy == "admin-1"));
    }

    [Test]
    public async Task Handle_WithUnknownCustomerId_ReturnsNotFoundAndPublishesNoEvent()
    {
        var repository = new FakeCustomerRepository();
        var messageBus = Substitute.For<IMessageBus>();
        var command = new DeactivateCustomerCommand(Guid.NewGuid(), "admin-1");

        var result = await DeactivateCustomerHandler.Handle(command, repository, Clock, messageBus, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(DeactivateCustomerStatus.NotFound);
        await messageBus.DidNotReceive().PublishAsync(Arg.Any<CustomerDeactivatedIntegrationEvent>());
    }
}
