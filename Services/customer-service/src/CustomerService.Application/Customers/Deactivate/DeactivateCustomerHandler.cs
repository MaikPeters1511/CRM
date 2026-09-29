using CustomerService.Application.Abstractions;
using CustomerService.Domain.Customers;
using Wolverine;

namespace CustomerService.Application.Customers.Deactivate;

public static class DeactivateCustomerHandler
{
    public static async Task<DeactivateCustomerResult> Handle(
        DeactivateCustomerCommand command,
        ICustomerRepository repository,
        TimeProvider timeProvider,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetByIdAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            return DeactivateCustomerResult.NotFound();
        }

        customer.Deactivate(command.DeactivatedBy, timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in customer.DomainEvents)
        {
            if (domainEvent is CustomerDeactivated deactivated)
            {
                await messageBus.PublishAsync(new CustomerDeactivatedIntegrationEvent(
                    deactivated.CustomerId,
                    deactivated.DeactivatedAt,
                    deactivated.DeactivatedBy));
            }
        }

        customer.ClearDomainEvents();

        return DeactivateCustomerResult.Success();
    }
}
