using IdentityService.Application.Auth.Login;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityService.Web.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(LoginHandler loginHandler) : ControllerBase
{
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
