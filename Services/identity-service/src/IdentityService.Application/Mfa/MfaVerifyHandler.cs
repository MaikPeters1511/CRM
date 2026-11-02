using IdentityService.Application.Audit;
using IdentityService.Application.Auth;
using IdentityService.Domain.Audit;

namespace IdentityService.Application.Mfa;

public sealed record MfaVerifyCommand(Guid UserId, string Code, string? IpAddress);

public enum MfaVerifyStatus
{
    Activated,
    InvalidCode,
    NoPendingSetup,
}

/// <summary>Schliesst die MFA-Einrichtung ab: verifiziert den ersten Code und aktiviert MFA fuer den Nutzer.</summary>
public sealed class MfaVerifyHandler(
    IMfaCredentialRepository credentialRepository,
    IUserRepository userRepository,
    ITotpProvider totpProvider,
    IAuditLogger auditLogger)
{
    public async Task<MfaVerifyStatus> HandleAsync(MfaVerifyCommand command, CancellationToken cancellationToken)
    {
        var credential = await credentialRepository.FindByUserIdAsync(command.UserId, cancellationToken);
        if (credential is null || credential.Verified)
        {
            return MfaVerifyStatus.NoPendingSetup;
        }

        if (!totpProvider.ValidateCode(credential.SecretBase32, command.Code))
        {
            return MfaVerifyStatus.InvalidCode;
        }

        credential.MarkVerified();
        await credentialRepository.SaveChangesAsync(cancellationToken);

        var user = await userRepository.FindByIdAsync(command.UserId, cancellationToken);
        if (user is not null)
        {
            user.EnableMfa();
            await userRepository.SaveChangesAsync(cancellationToken);
        }

        await auditLogger.LogAsync(
            AuditEventType.MfaEnabled, command.UserId, command.UserId, "MFA (TOTP) aktiviert.", command.IpAddress, cancellationToken);

        return MfaVerifyStatus.Activated;
    }
}
