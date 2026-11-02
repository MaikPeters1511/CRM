using IdentityService.Application.Mfa;

namespace IdentityService.Application.Auth.Login;

public sealed record CompleteMfaLoginCommand(string MfaChallengeToken, string Code, string IpAddress);

/// <summary>Zweiter Schritt des zweistufigen Logins bei aktiver MFA-Pflicht (<c>POST /api/auth/login/mfa</c>).</summary>
public sealed class CompleteMfaLoginHandler(
    ILoginChallengeCache loginChallengeCache,
    IUserRepository userRepository,
    IMfaCredentialRepository mfaCredentialRepository,
    ITotpProvider totpProvider,
    LoginHandler loginHandler)
{
    public async Task<LoginResult> HandleAsync(CompleteMfaLoginCommand command, CancellationToken cancellationToken)
    {
        var userId = loginChallengeCache.TryConsumeToken(command.MfaChallengeToken);
        if (userId is null)
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        var user = await userRepository.FindByIdAsync(userId.Value, cancellationToken);
        var credential = await mfaCredentialRepository.FindByUserIdAsync(userId.Value, cancellationToken);

        if (user is null || credential is null || !totpProvider.ValidateCode(credential.SecretBase32, command.Code))
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        return await loginHandler.IssueSuccessAsync(user, command.IpAddress, cancellationToken);
    }
}
