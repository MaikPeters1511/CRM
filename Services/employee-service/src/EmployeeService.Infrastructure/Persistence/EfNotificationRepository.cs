using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace EmployeeService.Infrastructure.Persistence;

public sealed class EfNotificationRepository(EmployeeDbContext dbContext) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken cancellationToken)
    {
        await dbContext.Notifications.AddAsync(notification, cancellationToken);
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetByRecipientAsync(Guid recipientEmployeeId, CancellationToken cancellationToken)
    {
        return await dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientEmployeeId == recipientEmployeeId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
