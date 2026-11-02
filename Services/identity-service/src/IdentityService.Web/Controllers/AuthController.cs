using IdentityService.Application.Auth.Login;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityService.Web.Controllers;

/// <summary>
/// Authentifizierung (identity.openspec.md, identity-mfa-totp.openspec.md,
/// identity-device-trust.openspec.md): zweistufiger Login-Handshake (Passwort -> ggf.
/// Device-Trust/MFA-Zwischenstatus), kein Access-Token vor Abschluss aller Gates
/// (docs/adr/0004, Abschnitt 5). Alle Endpoints hier sind <c>[AllowAnonymous]</c>, da sie Teil des
/// (noch) unauthentifizierten Login-Vorgangs sind.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(LoginHandler loginHandler, CompleteMfaLoginHandler completeMfaLoginHandler) : ControllerBase
{
    /// <summary>
    /// Prueft E-Mail/Passwort. Liefert bei Erfolg (keine Device-Bestaetigung/MFA erforderlich) ein
    /// signiertes JWT; andernfalls einen Zwischenstatus (<c>DeviceConfirmationRequired</c>/
    /// <c>MfaRequired</c>) ohne Token. Rate-Limited auf 5 Versuche/Minute/IP (<c>429</c>).
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await loginHandler.HandleAsync(
            new LoginCommand(request.Email, request.Password, UserAgent(), ClientIp()), cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Zweiter Schritt des zweistufigen Logins bei aktiver MFA-Pflicht.</summary>
    [HttpPost("login/mfa")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> LoginMfa([FromBody] LoginMfaRequest request, CancellationToken cancellationToken)
    {
        var result = await completeMfaLoginHandler.HandleAsync(
            new CompleteMfaLoginCommand(request.MfaChallengeToken, request.Code, ClientIp()), cancellationToken);

        return ToActionResult(result);
    }

    private IActionResult ToActionResult(LoginResult result) => result.Status switch
    {
        // Bewusst identisches Verhalten (kein Unterschied in Timing/Antwort) fuer
        // "Nutzer nicht gefunden" und "Passwort falsch", um User-Enumeration (OWASP A07) zu vermeiden.
        LoginStatus.InvalidCredentials => Unauthorized(new { error = "invalid_credentials" }),
        LoginStatus.Locked => StatusCode(StatusCodes.Status423Locked, new { error = "account_locked" }),
        LoginStatus.DeviceConfirmationRequired => Ok(new LoginChallengeResponse(
            "device_confirmation_required", null, result.PendingDeviceConfirmationId)),
        LoginStatus.MfaRequired => Ok(new LoginChallengeResponse("mfa_required", result.MfaChallengeToken, null)),
        _ => Ok(new LoginResponse(
            result.AccessToken!, result.ExpiresAtUtc!.Value, result.UserId!, result.Role!, result.DisplayName!)),
    };

    private string UserAgent() => Request.Headers.UserAgent.ToString();

    private string ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginMfaRequest(string MfaChallengeToken, string Code);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string UserId,
    string Role,
    string DisplayName);

/// <summary>Zwischenstatus-Antwort fuer <c>DeviceConfirmationRequired</c>/<c>MfaRequired</c> - niemals ein Access-Token.</summary>
public sealed record LoginChallengeResponse(string Status, string? MfaChallengeToken, Guid? PendingDeviceConfirmationId);
