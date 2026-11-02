using IdentityService.Domain.Audit;
using IdentityService.Domain.Security;
using IdentityService.Application.Audit;

namespace IdentityService.Application.Security;

public sealed record UpdateSecurityPolicyCommand(
    Guid ActorUserId,
    bool RequireMfa,
    int PasswordMinLength,
    int PasswordExpiryDays,
    int MaxFailedLoginAttempts,
    int LockoutDurationMinutes,
    string? IpAddress);

/// <summary>Admin-Endpunkt zum Setzen der org-weiten Sicherheitsrichtlinie (identity-device-trust.openspec.md, Abschnitt "Admin-Policy").</summary>
public sealed class UpdateSecurityPolicyHandler(ISecurityPolicyRepository repository, IAuditLogger auditLogger)
{
    public async Task<SecurityPolicy> HandleAsync(UpdateSecurityPolicyCommand command, CancellationToken cancellationToken)
    {
        var policy = await repository.GetAsync(cancellationToken);

        policy.Update(
            command.RequireMfa,
            command.PasswordMinLength,
            command.PasswordExpiryDays,
            command.MaxFailedLoginAttempts,
            command.LockoutDurationMinutes);

        await repository.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditEventType.SecurityPolicyChanged,
            command.ActorUserId,
            targetUserId: null,
            details: $"RequireMfa={command.RequireMfa}, PasswordMinLength={command.PasswordMinLength}, " +
                     $"PasswordExpiryDays={command.PasswordExpiryDays}, MaxFailedLoginAttempts={command.MaxFailedLoginAttempts}, " +
                     $"LockoutDurationMinutes={command.LockoutDurationMinutes}",
            command.IpAddress,
            cancellationToken);

        return policy;
    }
}
