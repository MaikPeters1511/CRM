using EmployeeService.Domain.Notifications;

namespace EmployeeService.Application.Abstractions;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken);

    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Neueste zuerst (abwesenheit-workflow.openspec.md, Abschnitt 5: GET /api/notifications).</summary>
    Task<IReadOnlyList<Notification>> GetByRecipientAsync(Guid recipientEmployeeId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
