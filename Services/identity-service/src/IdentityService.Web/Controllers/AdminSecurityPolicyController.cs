using IdentityService.Application.Security;
using IdentityService.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Web.Controllers;

/// <summary>Org-weite Sicherheitsrichtlinie: MFA-Pflicht, Passwort-Policy, Account-Lockout (identity-device-trust.openspec.md).</summary>
[ApiController]
[Route("api/admin/security-policy")]
[Authorize(Roles = "Admin")]
public sealed class AdminSecurityPolicyController(
    GetSecurityPolicyHandler getHandler,
    UpdateSecurityPolicyHandler updateHandler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var policy = await getHandler.HandleAsync(cancellationToken);
        return Ok(ToResponse(policy));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSecurityPolicyRequest request, CancellationToken cancellationToken)
    {
        var policy = await updateHandler.HandleAsync(
            new UpdateSecurityPolicyCommand(
                User.RequireUserId(), request.RequireMfa, request.PasswordMinLength, request.PasswordExpiryDays,
                request.MaxFailedLoginAttempts, request.LockoutDurationMinutes, HttpContext.ClientIp()),
            cancellationToken);

        return Ok(ToResponse(policy));
    }

    private static SecurityPolicyResponse ToResponse(SecurityPolicy policy) => new(
        policy.RequireMfa, policy.PasswordMinLength, policy.PasswordExpiryDays,
        policy.MaxFailedLoginAttempts, policy.LockoutDurationMinutes);
}

public sealed record UpdateSecurityPolicyRequest(
    bool RequireMfa, int PasswordMinLength, int PasswordExpiryDays, int MaxFailedLoginAttempts, int LockoutDurationMinutes);

public sealed record SecurityPolicyResponse(
    bool RequireMfa, int PasswordMinLength, int PasswordExpiryDays, int MaxFailedLoginAttempts, int LockoutDurationMinutes);
