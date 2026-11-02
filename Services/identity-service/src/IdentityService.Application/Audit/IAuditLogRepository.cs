using IdentityService.Domain.Audit;

namespace IdentityService.Application.Audit;

public sealed record AuditLogFilter(
    AuditEventType? EventType,
    Guid? UserId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc);

public sealed record AuditLogPage(IReadOnlyList<AuditLogEntry> Items, int TotalCount);

/// <summary>Lesender Zugriff auf das Audit-Log (identity-audit-log.openspec.md) fuer Admin-Abfrage/Export.</summary>
public interface IAuditLogRepository
{
    Task<AuditLogPage> QueryAsync(AuditLogFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Liefert alle zum Filter passenden Eintraege, unpaginiert, fuer den CSV-Export.</summary>
    Task<IReadOnlyList<AuditLogEntry>> QueryAllAsync(AuditLogFilter filter, CancellationToken cancellationToken);
}
