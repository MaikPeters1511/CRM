using Microsoft.AspNetCore.Http;

namespace AppointmentService.Infrastructure.Clients;

/// <summary>
/// Forwards the incoming request's <c>Authorization</c> header onto outgoing synchronous
/// service-to-service HTTP calls (Tests/E2E/README.md, Bug 8): <c>customer-service</c> and
/// <c>employee-service</c> are <c>[Authorize]</c>-protected and reject every call without a valid
/// JWT with <c>401</c> - regardless of whether the original (human) caller already authenticated
/// successfully against appointment-service. Request-forwarding (ADR 0001 Abschnitt 4, "Defense in
/// Depth": every service re-validates the JWT itself anyway) is the minimal-invasive fix; no
/// separate service-to-service credential flow is needed for the current scope (kein Gold-Plating).
///
/// KNOWN LIMITATION (by design): if the calling code does not run within the scope of an incoming
/// HTTP request (e.g. a Hosted Service/Background Job), <see cref="IHttpContextAccessor.HttpContext"/>
/// is <c>null</c> and no header is forwarded - the outgoing call then simply fails its own
/// <c>[Authorize]</c> check again, exactly as before this fix (no crash). Verified not to affect
/// <c>AppointmentReminderHostedService</c>: it only uses <c>IAppointmentRepository</c>/
/// <c>IReminderNotifier</c>, never one of the two synchronous HTTP clients affected by Bug 8.
/// </summary>
public sealed class AuthHeaderForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var incomingAuthorizationHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization;

        if (incomingAuthorizationHeader is { Count: > 0 } headerValues)
        {
            request.Headers.TryAddWithoutValidation("Authorization", headerValues.ToArray());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
