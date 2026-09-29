namespace IdentityService.Application.Auth.Login;

/// <summary>
/// Use-Case-Handler fuer den Login-Flow. Bewusst als einfache DI-Klasse implementiert
/// (kein Wolverine-Messaging wie in customer-service/employee-service), da der
/// Identity-Service keine Integration-Events publiziert und synchron antworten muss.
/// </summary>
public sealed class LoginHandler(
    IUserRepository userRepository,
    IPasswordVerifier passwordVerifier,
    ITokenGenerator tokenGenerator)
{
    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByEmailAsync(command.Email, cancellationToken);

        // Bewusst identisches Verhalten (kein Unterschied in Timing/Antwort) fuer
        // "Nutzer nicht gefunden" und "Passwort falsch", um User-Enumeration (OWASP A07) zu vermeiden.
        if (user is null || !passwordVerifier.Verify(user, command.Password))
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        var token = tokenGenerator.GenerateToken(user, out var expiresAtUtc);

        return new LoginResult(
            LoginStatus.Success,
            token,
            expiresAtUtc,
            user.Id.ToString(),
            user.Role.ToString(),
            user.DisplayName);
    }
}
