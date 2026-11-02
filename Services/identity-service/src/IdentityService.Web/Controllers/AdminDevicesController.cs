using IdentityService.Application.Devices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Web.Controllers;

/// <summary>Admin-Verwaltung bekannter Geraete je Mitarbeiter (identity-device-trust.openspec.md).</summary>
[ApiController]
[Route("api/admin/users/{userId:guid}/devices")]
[Authorize(Roles = "Admin")]
public sealed class AdminDevicesController(
    ListUserDevicesHandler listHandler,
    RevokeDeviceHandler revokeHandler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid userId, CancellationToken cancellationToken)
    {
        var devices = await listHandler.HandleAsync(new ListUserDevicesQuery(userId), cancellationToken);

        return Ok(devices.Select(d => new KnownDeviceResponse(
            d.Id, d.UserAgent, d.LastIpRange, d.IsRevoked, d.FirstSeenAtUtc, d.LastSeenAtUtc)));
    }

    [HttpPost("{deviceId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid userId, Guid deviceId, CancellationToken cancellationToken)
    {
        var status = await revokeHandler.HandleAsync(
            new RevokeDeviceCommand(User.RequireUserId(), userId, deviceId, HttpContext.ClientIp()), cancellationToken);

        return status == RevokeDeviceStatus.Revoked ? Ok() : NotFound();
    }
}

public sealed record KnownDeviceResponse(
    Guid Id, string UserAgent, string LastIpRange, bool IsRevoked, DateTimeOffset FirstSeenAtUtc, DateTimeOffset LastSeenAtUtc);
