using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Employees;
using EmployeeService.Domain.Notifications;
using Wolverine;

namespace EmployeeService.Application.Employees.RejectAbsence;

public static class RejectAbsenceHandler
{
    public static async Task<RejectAbsenceResult> Handle(
        RejectAbsenceCommand command,
        IEmployeeRepository employeeRepository,
        INotificationRepository notificationRepository,
        TimeProvider timeProvider,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return RejectAbsenceResult.NotFound();
        }

        var decidedAt = timeProvider.GetUtcNow();
        var decision = employee.RejectAbsence(command.AbsenceId, command.DecidedBy, decidedAt);

        if (decision.Status == AbsenceDecisionStatus.NotFound)
        {
            return RejectAbsenceResult.NotFound();
        }

        if (decision.Status == AbsenceDecisionStatus.Conflict)
        {
            return RejectAbsenceResult.Conflict(decision.Errors);
        }

        var absence = decision.Absence!;

        var notification = Notification.Create(
            Guid.NewGuid(),
            employee.Id,
            $"Deine Abwesenheitsanfrage ({absence.Type}, {absence.From:yyyy-MM-dd} - {absence.To:yyyy-MM-dd}) wurde abgelehnt.",
            $"/employees/{employee.Id}/absences/{absence.Id}",
            decidedAt);

        await notificationRepository.AddAsync(notification, cancellationToken);
        await employeeRepository.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in employee.DomainEvents)
        {
            if (domainEvent is AbsenceRejected rejected)
            {
                await messageBus.PublishAsync(new AbsenceRejectedIntegrationEvent(
                    rejected.AbsenceId, rejected.EmployeeId, rejected.RejectedBy, rejected.RejectedAt));
            }
        }

        employee.ClearDomainEvents();

        return RejectAbsenceResult.Success(AbsenceDto.FromDomain(absence));
    }
}
