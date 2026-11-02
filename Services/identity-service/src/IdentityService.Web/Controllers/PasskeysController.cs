using IdentityService.Application.Passkeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Web.Controllers;

/// <summary>Passkey-Verwaltung (WebAuthn/FIDO2) im eigenen Profil (identity-passkeys-webauthn.openspec.md).</summary>
[ApiController]
[Route("api/auth/passkeys")]
[Authorize]
public sealed class PasskeysController(
    BeginPasskeyRegistrationHandler beginRegistrationHandler,
    CompletePasskeyRegistrationHandler completeRegistrationHandler,
    ListPasskeysHandler listHandler,
    RemovePasskeyHandler removeHandler) : ControllerBase
{
    [HttpPost("register/options")]
    public async Task<IActionResult> RegisterOptions(CancellationToken cancellationToken)
    {
        var optionsJson = await beginRegistrationHandler.HandleAsync(
            new BeginPasskeyRegistrationCommand(User.RequireUserId()), cancellationToken);

        return Content(optionsJson, "application/json");
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterPasskeyRequest request, CancellationToken cancellationToken)
    {
        var credential = await completeRegistrationHandler.HandleAsync(
            new CompletePasskeyRegistrationCommand(User.RequireUserId(), request.AttestationResponseJson, request.Name, HttpContext.ClientIp()),
            cancellationToken);

        return Ok(new PasskeyResponse(credential.Id, credential.Name, credential.CreatedAtUtc, credential.LastUsedAtUtc));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var credentials = await listHandler.HandleAsync(new ListPasskeysQuery(User.RequireUserId()), cancellationToken);

        return Ok(credentials.Select(c => new PasskeyResponse(c.Id, c.Name, c.CreatedAtUtc, c.LastUsedAtUtc)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        var status = await removeHandler.HandleAsync(
            new RemovePasskeyCommand(User.RequireUserId(), id, HttpContext.ClientIp()), cancellationToken);

        return status == RemovePasskeyStatus.Removed ? NoContent() : NotFound();
    }
}

public sealed record RegisterPasskeyRequest(string AttestationResponseJson, string Name);

public sealed record PasskeyResponse(Guid Id, string Name, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastUsedAtUtc);
