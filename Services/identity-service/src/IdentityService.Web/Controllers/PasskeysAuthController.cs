using IdentityService.Application.Auth.Login;
using IdentityService.Application.Passkeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Web.Controllers;

/// <summary>
/// Passwortloser Login ueber Passkeys (identity-passkeys-webauthn.openspec.md) - bewusst von
/// <see cref="PasskeysController"/> getrennt, da diese Endpoints (wie <c>AuthController</c>)
/// <c>[AllowAnonymous]</c> sind (noch kein authentifizierter Nutzer).
/// </summary>
[ApiController]
[Route("api/auth/passkeys/login")]
[AllowAnonymous]
public sealed class PasskeysAuthController(
    BeginPasskeyLoginHandler beginLoginHandler,
    CompletePasskeyLoginHandler completeLoginHandler) : ControllerBase
{
    [HttpPost("options")]
    public async Task<IActionResult> Options(CancellationToken cancellationToken)
    {
        var optionsJson = await beginLoginHandler.HandleAsync(cancellationToken);
        return Content(optionsJson, "application/json");
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] CompletePasskeyLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await completeLoginHandler.HandleAsync(
            new CompletePasskeyLoginCommand(request.AssertionResponseJson, HttpContext.UserAgent(), HttpContext.ClientIp()),
            cancellationToken);

        return result.Status switch
        {
            LoginStatus.InvalidCredentials => Unauthorized(new { error = "invalid_credentials" }),
            LoginStatus.DeviceConfirmationRequired => Ok(new LoginChallengeResponse(
                "device_confirmation_required", null, result.PendingDeviceConfirmationId)),
            LoginStatus.MfaRequired => Ok(new LoginChallengeResponse("mfa_required", result.MfaChallengeToken, null)),
            _ => Ok(new LoginResponse(
                result.AccessToken!, result.ExpiresAtUtc!.Value, result.UserId!, result.Role!, result.DisplayName!)),
        };
    }
}

public sealed record CompletePasskeyLoginRequest(string AssertionResponseJson);
