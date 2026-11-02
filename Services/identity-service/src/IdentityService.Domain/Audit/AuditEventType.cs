namespace IdentityService.Domain.Audit;

/// <summary>Erfasste sicherheitsrelevante und fachliche Ereignistypen (identity-audit-log.openspec.md).</summary>
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

    CustomerCreated,
    CustomerUpdated,
    CustomerDeactivated,
    EmployeeCreated,
    EmployeeUpdated,
    EmployeeDeactivated,
    WorkingHoursUpdated,
    AbsenceCreated,
    AbsenceApproved,
    AbsenceRejected,
    AbsenceDeleted,
    AppointmentCreated,
    AppointmentRescheduled,
    AppointmentCancelled,
    ServiceCreated,
    ServiceUpdated,
    ServiceDeactivated,
    DataChanged,
}
