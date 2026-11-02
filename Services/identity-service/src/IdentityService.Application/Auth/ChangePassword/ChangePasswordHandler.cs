using IdentityService.Application.Audit;
using IdentityService.Application.Security;
using IdentityService.Domain.Audit;

namespace IdentityService.Application.Auth.ChangePassword;

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword, string? IpAddress);

public enum ChangePasswordStatus
{
    Changed,
    InvalidCurrentPassword,
    PolicyViolation,
}

/// <summary>Durchsetzt die Passwort-Mindestlaenge aus <c>SecurityPolicy</c> beim Passwort-Aendern (identity-device-trust.openspec.md).</summary>
public sealed class ChangePasswordHandler(
    IUserRepository userRepository,
    IPasswordVerifier passwordVerifier,
    ISecurityPolicyRepository securityPolicyRepository,
    IAuditLogger auditLogger)
{
    public async Task<ChangePasswordStatus> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null || !passwordVerifier.Verify(user, command.CurrentPassword))
        {
            return ChangePasswordStatus.InvalidCurrentPassword;
        }

        var policy = await securityPolicyRepository.GetAsync(cancellationToken);
        if (command.NewPassword.Length < policy.PasswordMinLength)
        {
            return ChangePasswordStatus.PolicyViolation;
        }

        user.SetPasswordHash(passwordVerifier.Hash(user, command.NewPassword));
        await userRepository.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditEventType.PasswordChanged, command.UserId, command.UserId, "Passwort geaendert.",
            command.IpAddress, cancellationToken);

        return ChangePasswordStatus.Changed;
    }
}
