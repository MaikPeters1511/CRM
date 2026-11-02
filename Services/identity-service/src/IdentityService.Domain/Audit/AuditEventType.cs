namespace IdentityService.Domain.Audit;

/// <summary>Erfasste sicherheitsrelevante Ereignistypen (identity-audit-log.openspec.md).</summary>
public enum AuditEventType
{
    LoginSucceeded,
    LoginFailed,
    AccountLocked,
    RoleChanged,
    PasswordChanged,
    MfaEnabled,
    MfaDisabled,
    PasskeyRegistered,
    PasskeyRemoved,
    DeviceConfirmationRequested,
    DeviceApproved,
    DeviceRevoked,
    SecurityPolicyChanged,

    /// <summary>Optional: aktuell existiert im Projekt kein API-Key-Rotationsmechanismus; vorgesehen fuer Folge-Changes.</summary>
    ApiKeyRotated,
}
