using IdentityService.Domain.Audit;

namespace IdentityService.Application.Audit;

/// <summary>
/// Gemeinsame Grundlage fuer alle vier Identity-Security-Changes (identity-audit-log.openspec.md,
/// docs/adr/0004 Abschnitt 6): schreibt synchron einen append-only Audit-Log-Eintrag.
/// Application-Interface (kein Domain-Konzept - Logging ist ein Application-Concern).
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        AuditEventType eventType,
        Guid actorUserId,
        Guid? targetUserId,
        string details,
        string? ipAddress,
        CancellationToken cancellationToken);
}
