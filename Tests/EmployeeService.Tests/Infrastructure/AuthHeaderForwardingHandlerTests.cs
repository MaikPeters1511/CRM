using EmployeeService.Infrastructure.Clients;
using Microsoft.AspNetCore.Http;

namespace EmployeeService.Tests.Infrastructure;

/// <summary>
/// Regression tests for Tests/E2E/README.md, Bug 8 ("Service-zu-Service-HTTP-Clients senden
/// KEINEN Authorization-Header"). Verifies <see cref="AuthHeaderForwardingHandler"/> forwards the
/// incoming request's Authorization header onto the outgoing HttpClient request, without starting
/// a real downstream service (captures the outgoing request via a fake innermost handler).
/// </summary>
public sealed class AuthHeaderForwardingHandlerTests
{
    [Test]
    public async Task SendAsync_WithIncomingBearerToken_ForwardsAuthorizationHeaderToOutgoingRequest()
    {
        // GIVEN an incoming HTTP request carrying a Bearer token
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext!.Request.Headers.Authorization = "Bearer valid-token";

        var capturingHandler = new CapturingHttpMessageHandler();
        var handler = new AuthHeaderForwardingHandler(httpContextAccessor) { InnerHandler = capturingHandler };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://appointment-service/api/appointments");

        // WHEN the outgoing service-to-service call is sent
        await invoker.SendAsync(request, CancellationToken.None);

        // THEN the outgoing request carries the same Authorization header as the incoming request
        await Assert.That(capturingHandler.CapturedRequest).IsNotNull();
        await Assert.That(capturingHandler.CapturedRequest!.Headers.Authorization?.ToString())
            .IsEqualTo("Bearer valid-token");
    }

    [Test]
    public async Task SendAsync_WithoutIncomingHttpContext_DoesNotCrashAndSendsNoAuthorizationHeader()
    {
        // GIVEN no incoming HTTP request in scope (e.g. a Hosted Service/Background Job)
        var httpContextAccessor = new HttpContextAccessor { HttpContext = null };

        var capturingHandler = new CapturingHttpMessageHandler();
        var handler = new AuthHeaderForwardingHandler(httpContextAccessor) { InnerHandler = capturingHandler };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://appointment-service/api/appointments");

        // WHEN the outgoing call is sent
        await invoker.SendAsync(request, CancellationToken.None);

        // THEN the call still goes through, just without an Authorization header
        await Assert.That(capturingHandler.CapturedRequest).IsNotNull();
        await Assert.That(capturingHandler.CapturedRequest!.Headers.Authorization).IsNull();
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
