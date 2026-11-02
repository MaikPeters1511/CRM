using IdentityService.Application.Audit;
using IdentityService.Domain.Audit;
using IdentityService.Infrastructure.Persistence;

namespace IdentityService.Infrastructure.Audit;

/// <summary>Synchrone, transaktionale DB-Writes gegen die append-only Tabelle (docs/adr/0004, Abschnitt 6).</summary>
public sealed class EfAuditLogger(IdentityDbContext dbContext) : IAuditLogger
{
    public async Task LogAsync(
        AuditEventType eventType, Guid actorUserId, Guid? targetUserId, string details, string? ipAddress,
        CancellationToken cancellationToken)
    {
        var entry = new AuditLogEntry(Guid.NewGuid(), eventType, actorUserId, targetUserId, details, ipAddress);
        await dbContext.AuditLogEntries.AddAsync(entry, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
