using Microsoft.AspNetCore.Http;

namespace AiService.Infrastructure.Clients;

/// <summary>
/// Reicht den eingehenden Authorization-Header an ausgehende synchrone Service-zu-Service-Calls
/// weiter (identisches Muster wie AppointmentService.Infrastructure.Clients.AuthHeaderForwardingHandler),
/// da appointment-service/employee-service/catalog-service/customer-service [Authorize]-geschuetzt
/// sind und RBAC serverseitig in ai-service anhand dieses Tokens erzwungen wird.
///
/// Laeuft der Aufruf NICHT im Scope eines eingehenden HTTP-Requests (Wolverine-Event-Konsumenten/
/// taeglicher Sync-Job, siehe AiService.Application.IndexSync), ist
/// <see cref="IHttpContextAccessor.HttpContext"/> <c>null</c>; dann wird das Token des technischen
/// Service-Accounts (<see cref="IServiceAccountTokenProvider"/>) verwendet. Ist keiner konfiguriert,
/// schlaegt der Call mit 401 fehl statt abzustuerzen.
/// </summary>
public sealed class AuthHeaderForwardingHandler(
    IHttpContextAccessor httpContextAccessor,
    IServiceAccountTokenProvider? serviceAccountTokenProvider = null) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var incomingAuthorizationHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization;

        if (incomingAuthorizationHeader is { Count: > 0 } headerValues)
        {
            request.Headers.TryAddWithoutValidation("Authorization", headerValues.ToArray());
        }
        else if (serviceAccountTokenProvider is not null &&
                 await serviceAccountTokenProvider.GetTokenAsync(cancellationToken) is { } serviceToken)
        {
            // Hintergrundprozess ohne Nutzerkontext: technischer Service-Account (ADR 0003).
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {serviceToken}");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
