using IdentityService.Application.Audit;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public sealed class EfAuditLogRepository(IdentityDbContext dbContext) : IAuditLogRepository
{
    public async Task<AuditLogPage> QueryAsync(AuditLogFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = ApplyFilter(filter);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AuditLogPage(items, totalCount);
    }

    public async Task<IReadOnlyList<Domain.Audit.AuditLogEntry>> QueryAllAsync(AuditLogFilter filter, CancellationToken cancellationToken) =>
        await ApplyFilter(filter).OrderByDescending(e => e.OccurredAtUtc).ToListAsync(cancellationToken);

    private IQueryable<Domain.Audit.AuditLogEntry> ApplyFilter(AuditLogFilter filter)
    {
        IQueryable<Domain.Audit.AuditLogEntry> query = dbContext.AuditLogEntries.AsNoTracking();

        if (filter.EventType is { } eventType)
        {
            query = query.Where(e => e.EventType == eventType);
        }

        if (filter.UserId is { } userId)
        {
            query = query.Where(e => e.ActorUserId == userId || e.TargetUserId == userId);
        }

        if (filter.FromUtc is { } from)
        {
            query = query.Where(e => e.OccurredAtUtc >= from);
        }

        if (filter.ToUtc is { } to)
        {
            query = query.Where(e => e.OccurredAtUtc <= to);
        }

        return query;
    }
}
