using IdentityService.Application.Audit;
using IdentityService.Application.Auth;
using IdentityService.Domain.Audit;

namespace IdentityService.Application.Mfa;

public sealed record MfaDisableCommand(Guid UserId, string CurrentPassword, string? IpAddress);

public enum MfaDisableStatus
{
    Disabled,
    InvalidPassword,
    NotEnabled,
}

/// <summary>Deaktiviert MFA; erfordert das aktuelle Passwort (identity-mfa-totp.openspec.md).</summary>
public sealed class MfaDisableHandler(
    IUserRepository userRepository,
    IMfaCredentialRepository credentialRepository,
    IPasswordVerifier passwordVerifier,
    IAuditLogger auditLogger)
{
    public async Task<MfaDisableStatus> HandleAsync(MfaDisableCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null || !passwordVerifier.Verify(user, command.CurrentPassword))
        {
            return MfaDisableStatus.InvalidPassword;
        }

        if (!user.MfaEnabled)
        {
            return MfaDisableStatus.NotEnabled;
        }

        var credential = await credentialRepository.FindByUserIdAsync(command.UserId, cancellationToken);
        if (credential is not null)
        {
            credentialRepository.Remove(credential);
            await credentialRepository.SaveChangesAsync(cancellationToken);
        }

        user.DisableMfa();
        await userRepository.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditEventType.MfaDisabled, command.UserId, command.UserId, "MFA (TOTP) deaktiviert.", command.IpAddress, cancellationToken);

        return MfaDisableStatus.Disabled;
    }
}
