using IdentityService.Application.Devices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Web.Controllers;

/// <summary>
/// Geraete-Bestaetigung (identity-device-trust.openspec.md). <c>[AllowAnonymous]</c> wie
/// <c>AuthController</c>, da zum Zeitpunkt der Bestaetigung noch kein Access-Token vorliegt.
/// </summary>
[ApiController]
[Route("api/auth/devices")]
[AllowAnonymous]
public sealed class DevicesController(ConfirmDeviceHandler confirmHandler) : ControllerBase
{
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmDeviceRequest request, CancellationToken cancellationToken)
    {
        var result = await confirmHandler.HandleAsync(
            new ConfirmDeviceCommand(request.PendingConfirmationId, request.Code, HttpContext.ClientIp()), cancellationToken);

        return result.Status switch
        {
            ConfirmDeviceStatus.Confirmed => Ok(),
            ConfirmDeviceStatus.InvalidCode => BadRequest(new { error = "invalid_code" }),
            ConfirmDeviceStatus.Expired => BadRequest(new { error = "expired" }),
            _ => NotFound(),
        };
    }
}

public sealed record ConfirmDeviceRequest(Guid PendingConfirmationId, string Code);
