using System.Security.Claims;

namespace IdentityService.Web.Controllers;

internal static class CurrentUserExtensions
{
    public static Guid RequireUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static string ClientIp(this HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static string UserAgent(this HttpContext context) => context.Request.Headers.UserAgent.ToString();
}
