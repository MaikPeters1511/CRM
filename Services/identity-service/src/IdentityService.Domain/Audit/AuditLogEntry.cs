namespace IdentityService.Domain.Audit;

/// <summary>
/// Append-only Audit-Log-Eintrag (identity-audit-log.openspec.md). Es gibt bewusst keine
/// Update-/Delete-Methoden - Unveraenderlichkeit wird auf Anwendungsebene durch Fehlen
/// entsprechender Repository-Methoden erzwungen (docs/adr/0004, Abschnitt 6).
/// </summary>
public sealed class AuditLogEntry
{
    public AuditLogEntry(
        Guid id,
        AuditEventType eventType,
        Guid actorUserId,
        Guid? targetUserId,
        string details,
        string? ipAddress)
    {
        Id = id;
        EventType = eventType;
        ActorUserId = actorUserId;
        TargetUserId = targetUserId;
        Details = details;
        IpAddress = ipAddress;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    private AuditLogEntry()
    {
        Details = string.Empty;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public AuditEventType EventType { get; private set; }

    public Guid ActorUserId { get; private set; }

    public Guid? TargetUserId { get; private set; }

    public string Details { get; private set; }

    public string? IpAddress { get; private set; }
}
