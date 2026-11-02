using IdentityService.Application.Auth;
using IdentityService.Application.Auth.Login;

namespace IdentityService.Application.Passkeys;

public sealed class BeginPasskeyLoginHandler(IFido2CeremonyService ceremonyService)
{
    public Task<string> HandleAsync(CancellationToken cancellationToken) =>
        ceremonyService.BeginAssertionAsync(cancellationToken);
}

public sealed record CompletePasskeyLoginCommand(string AssertionResponseJson, string UserAgent, string IpAddress);

/// <summary>
/// Passwortloser Login ueber Passkeys (identity-passkeys-webauthn.openspec.md): durchlaeuft ab der
/// erfolgreichen Assertion-Validierung dieselben Device-Trust-/MFA-Gates wie der passwortbasierte
/// Flow (docs/adr/0004, Abschnitt 5, Punkt 5) - kein Lockout-Check, da kein Passwort-Fehlversuchszaehler greift.
/// </summary>
public sealed class CompletePasskeyLoginHandler(
    IFido2CeremonyService ceremonyService,
    IPasskeyCredentialRepository credentialRepository,
    IUserRepository userRepository,
    LoginHandler loginHandler,
    TimeProvider timeProvider)
{
    public async Task<LoginResult> HandleAsync(CompletePasskeyLoginCommand command, CancellationToken cancellationToken)
    {
        var assertion = await ceremonyService.CompleteAssertionAsync(command.AssertionResponseJson, cancellationToken);

        var credential = await credentialRepository.FindByCredentialIdAsync(assertion.CredentialId, cancellationToken);
        if (credential is null)
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        credential.UpdateSignCount(assertion.NewSignCount);
        await credentialRepository.SaveChangesAsync(cancellationToken);

        var user = await userRepository.FindByIdAsync(credential.UserId, cancellationToken);
        if (user is null)
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        return await loginHandler.ContinueAfterPasswordVerifiedAsync(
            user, command.UserAgent, command.IpAddress, timeProvider.GetUtcNow(), cancellationToken);
    }
}
