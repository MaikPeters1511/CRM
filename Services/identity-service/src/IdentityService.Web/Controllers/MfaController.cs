using IdentityService.Application.Mfa;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Web.Controllers;

/// <summary>Zwei-Faktor-Authentifizierung (TOTP) im eigenen Profil (identity-mfa-totp.openspec.md).</summary>
[ApiController]
[Route("api/auth/mfa")]
[Authorize]
public sealed class MfaController(
    MfaSetupHandler setupHandler,
    MfaVerifyHandler verifyHandler,
    MfaDisableHandler disableHandler) : ControllerBase
{
    [HttpPost("setup")]
    public async Task<IActionResult> Setup(CancellationToken cancellationToken)
    {
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty;
        var result = await setupHandler.HandleAsync(
            new MfaSetupCommand(User.RequireUserId(), email), cancellationToken);

        return Ok(new MfaSetupResponse(result.SecretBase32, result.OtpAuthUri));
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] MfaVerifyRequest request, CancellationToken cancellationToken)
    {
        var status = await verifyHandler.HandleAsync(
            new MfaVerifyCommand(User.RequireUserId(), request.Code, HttpContext.ClientIp()), cancellationToken);

        return status switch
        {
            MfaVerifyStatus.Activated => Ok(),
            MfaVerifyStatus.InvalidCode => BadRequest(new { error = "invalid_code" }),
            _ => BadRequest(new { error = "no_pending_setup" }),
        };
    }

    [HttpDelete]
    public async Task<IActionResult> Disable([FromBody] MfaDisableRequest request, CancellationToken cancellationToken)
    {
        var status = await disableHandler.HandleAsync(
            new MfaDisableCommand(User.RequireUserId(), request.CurrentPassword, HttpContext.ClientIp()), cancellationToken);

        return status switch
        {
            MfaDisableStatus.Disabled => Ok(),
            MfaDisableStatus.InvalidPassword => Unauthorized(new { error = "invalid_password" }),
            _ => BadRequest(new { error = "not_enabled" }),
        };
    }
}

public sealed record MfaSetupResponse(string SecretBase32, string OtpAuthUri);

public sealed record MfaVerifyRequest(string Code);

public sealed record MfaDisableRequest(string CurrentPassword);
