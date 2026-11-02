using IdentityService.Application.Auth.ChangePassword;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Web.Controllers;

/// <summary>Eigenes Passwort aendern, unter Durchsetzung der Passwort-Richtlinie (identity-device-trust.openspec.md).</summary>
[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController(ChangePasswordHandler changePasswordHandler) : ControllerBase
{
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var status = await changePasswordHandler.HandleAsync(
            new ChangePasswordCommand(User.RequireUserId(), request.CurrentPassword, request.NewPassword, HttpContext.ClientIp()),
            cancellationToken);

        return status switch
        {
            ChangePasswordStatus.Changed => Ok(),
            ChangePasswordStatus.InvalidCurrentPassword => Unauthorized(new { error = "invalid_current_password" }),
            _ => BadRequest(new { error = "password_policy_violation" }),
        };
    }
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
