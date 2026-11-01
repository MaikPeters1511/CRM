using Microsoft.AspNetCore.Http;

namespace AiService.Infrastructure.Clients;

/// <summary>
/// Reicht den eingehenden Authorization-Header an ausgehende synchrone Service-zu-Service-Calls
/// weiter (identisches Muster wie AppointmentService.Infrastructure.Clients.AuthHeaderForwardingHandler),
/// da appointment-service/employee-service/catalog-service/customer-service [Authorize]-geschuetzt
/// sind und RBAC serverseitig in ai-service anhand dieses Tokens erzwungen wird.
///
/// KNOWN LIMITATION (wie im Vorbild appointment-service): laeuft der Aufruf NICHT im Scope eines
/// eingehenden HTTP-Requests (Wolverine-Event-Konsumenten/taeglicher Sync-Job, siehe
/// AiService.Application.IndexSync), ist <see cref="IHttpContextAccessor.HttpContext"/> <c>null</c>
/// und kein Header wird weitergereicht - der Call schlaegt dann mit 401 fehl statt abzustuerzen.
/// Fuer den aktuellen, reinen Ableitungsdaten-Scope bewusst akzeptiert (kein Gold-Plating durch
/// einen noch nicht etablierten Service-Account-Mechanismus) - siehe ADR 0003, Folgeaufgaben.
/// </summary>
public sealed class AuthHeaderForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var incomingAuthorizationHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization;

        if (incomingAuthorizationHeader is { Count: > 0 } headerValues)
        {
            request.Headers.TryAddWithoutValidation("Authorization", headerValues.ToArray());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
