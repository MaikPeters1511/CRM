using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Notifications;

namespace EmployeeService.Tests.Support;

public sealed class FakeNotificationRepository : INotificationRepository
{
    private readonly List<Notification> _notifications = [];

    public IReadOnlyList<Notification> Notifications => _notifications;

    public int SaveChangesCallCount { get; private set; }

    public void Seed(Notification notification) => _notifications.Add(notification);

    public Task AddAsync(Notification notification, CancellationToken cancellationToken)
    {
        _notifications.Add(notification);
        return Task.CompletedTask;
    }

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_notifications.FirstOrDefault(n => n.Id == id));
    }

    public Task<IReadOnlyList<Notification>> GetByRecipientAsync(Guid recipientEmployeeId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Notification> result = _notifications
            .Where(n => n.RecipientEmployeeId == recipientEmployeeId)
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        return Task.FromResult(result);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
