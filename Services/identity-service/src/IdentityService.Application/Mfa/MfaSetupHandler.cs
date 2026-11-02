using IdentityService.Domain.Mfa;

namespace IdentityService.Application.Mfa;

public sealed record MfaSetupCommand(Guid UserId, string AccountEmail);

public sealed record MfaSetupResult(string SecretBase32, string OtpAuthUri);

/// <summary>
/// Startet die MFA-Einrichtung (identity-mfa-totp.openspec.md): generiert ein neues Secret und
/// legt ein noch unverifiziertes <see cref="MfaCredential"/> an. Ein vorhandenes, noch nicht
/// verifiziertes Credential wird ersetzt (z.B. abgebrochener vorheriger Setup-Versuch); ein
/// bereits verifiziertes Credential verhindert einen erneuten Setup (zuerst deaktivieren).
/// </summary>
public sealed class MfaSetupHandler(
    IMfaCredentialRepository repository,
    ITotpProvider totpProvider,
    IssuerOptions issuer)
{
    public async Task<MfaSetupResult> HandleAsync(MfaSetupCommand command, CancellationToken cancellationToken)
    {
        var existing = await repository.FindByUserIdAsync(command.UserId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Verified)
            {
                throw new InvalidOperationException("MFA ist bereits aktiviert. Zuerst deaktivieren, um neu einzurichten.");
            }

            repository.Remove(existing);
        }

        var secret = totpProvider.GenerateSecret();
        var credential = new MfaCredential(Guid.NewGuid(), command.UserId, secret);
        await repository.AddAsync(credential, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var otpAuthUri = totpProvider.BuildOtpAuthUri(secret, command.AccountEmail, issuer.Name);
        return new MfaSetupResult(secret, otpAuthUri);
    }
}

/// <summary>Issuer-Name fuer die otpauth://-URI (z.B. "CRM"), aus Konfiguration, kein Hardcoding im Handler.</summary>
public sealed record IssuerOptions(string Name);
