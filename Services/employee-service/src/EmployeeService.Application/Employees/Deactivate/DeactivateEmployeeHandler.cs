using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Employees;
using Wolverine;

namespace EmployeeService.Application.Employees.Deactivate;

public static class DeactivateEmployeeHandler
{
    public static async Task<DeactivateEmployeeResult> Handle(
        DeactivateEmployeeCommand command,
        IEmployeeRepository repository,
        TimeProvider timeProvider,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(command.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return DeactivateEmployeeResult.NotFound();
        }

        employee.Deactivate(command.DeactivatedBy, timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in employee.DomainEvents)
        {
            if (domainEvent is EmployeeDeactivated deactivated)
            {
                await messageBus.PublishAsync(new EmployeeDeactivatedIntegrationEvent(
                    deactivated.EmployeeId,
                    deactivated.DeactivatedAt,
                    deactivated.DeactivatedBy));
            }
        }

        employee.ClearDomainEvents();

        return DeactivateEmployeeResult.Success();
    }
}
