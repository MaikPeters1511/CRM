using IdentityService.Application.Auth.Login;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityService.Web.Controllers;

/// <summary>
/// Authentifizierung (authentifizierung.openspec.md): schlanker, selbst gehosteter JWT-Issuer,
/// kein OIDC-Discovery/externer IdP. Alle Endpoints sind <c>[AllowAnonymous]</c>.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(LoginHandler loginHandler) : ControllerBase
{
    /// <summary>
    /// Prueft E-Mail/Passwort gegen die geseedeten Nutzer und liefert bei Erfolg ein signiertes JWT
    /// (Claims <c>NameIdentifier</c>/<c>Role</c>). Liefert <c>401</c> bei falschem Passwort oder
    /// unbekannter E-Mail (identisches Verhalten, verhindert User-Enumeration). Rate-Limited auf
    /// 5 Versuche/Minute/IP (<c>429</c> bei Ueberschreitung).
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await loginHandler.HandleAsync(
            new LoginCommand(request.Email, request.Password), cancellationToken);

        if (result.Status == LoginStatus.InvalidCredentials)
        {
            // Bewusst generische Fehlermeldung (kein "User nicht gefunden" vs. "Passwort falsch"),
            // um User-Enumeration zu vermeiden (OWASP A07: Identification and Authentication Failures).
            return Unauthorized(new { error = "invalid_credentials" });
        }

        return Ok(new LoginResponse(
            result.AccessToken!,
            result.ExpiresAtUtc!.Value,
            result.UserId!,
            result.Role!,
            result.DisplayName!));
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string UserId,
    string Role,
    string DisplayName);
