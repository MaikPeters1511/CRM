using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Employees;
using EmployeeService.Domain.Notifications;
using Wolverine;

namespace EmployeeService.Application.Employees.ApproveAbsence;

public static class ApproveAbsenceHandler
{
    public static async Task<ApproveAbsenceResult> Handle(
        ApproveAbsenceCommand command,
        IEmployeeRepository employeeRepository,
        INotificationRepository notificationRepository,
        TimeProvider timeProvider,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return ApproveAbsenceResult.NotFound();
        }

        var decidedAt = timeProvider.GetUtcNow();
        var decision = employee.ApproveAbsence(command.AbsenceId, command.DecidedBy, decidedAt);

        if (decision.Status == AbsenceDecisionStatus.NotFound)
        {
            return ApproveAbsenceResult.NotFound();
        }

        if (decision.Status == AbsenceDecisionStatus.Conflict)
        {
            return ApproveAbsenceResult.Conflict(decision.Errors);
        }

        var absence = decision.Absence!;

        // Gleicher Bounded Context (abwesenheit-workflow.openspec.md, Abschnitt 7): Notification wird
        // per direktem Repository-Call angelegt, kein Messaging nötig für diesen In-Process-Teil.
        var notification = Notification.Create(
            Guid.NewGuid(),
            employee.Id,
            $"Deine Abwesenheitsanfrage ({absence.Type}, {absence.From:yyyy-MM-dd} - {absence.To:yyyy-MM-dd}) wurde genehmigt.",
            $"/employees/{employee.Id}/absences/{absence.Id}",
            decidedAt);

        await notificationRepository.AddAsync(notification, cancellationToken);

        // Einziger SaveChanges-Aufruf: Employee-Repository und Notification-Repository teilen sich
        // denselben DbContext (EmployeeDbContext), daher werden beide Aenderungen hier zusammen
        // persistiert (siehe EmployeeService.Infrastructure.Persistence.EfNotificationRepository).
        await employeeRepository.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in employee.DomainEvents)
        {
            if (domainEvent is AbsenceApproved approved)
            {
                await messageBus.PublishAsync(new AbsenceApprovedIntegrationEvent(
                    approved.AbsenceId, approved.EmployeeId, approved.From, approved.To, approved.Type,
                    approved.ApprovedBy, approved.ApprovedAt));
            }
        }

        employee.ClearDomainEvents();

        return ApproveAbsenceResult.Success(AbsenceDto.FromDomain(absence));
    }
}
