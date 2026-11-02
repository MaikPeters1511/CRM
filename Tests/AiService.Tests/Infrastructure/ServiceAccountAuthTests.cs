using System.Net;
using System.Text;
using AiService.Infrastructure.Clients;
using AiService.Tests.Support;
using Microsoft.AspNetCore.Http;

namespace AiService.Tests.Infrastructure;

public class ServiceAccountAuthTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage LoginOk(string token, DateTimeOffset expires) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"accessToken\":\"{token}\",\"expiresAtUtc\":\"{expires:O}\",\"userId\":\"u\",\"role\":\"Admin\",\"displayName\":\"x\"}}",
                Encoding.UTF8, "application/json"),
        };

    private static ServiceAccountTokenProvider CreateProvider(StubHandler handler, string? password = "pw") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://identity") },
            new ServiceAccountOptions { Email = "ai-service@crm.local", Password = password },
            new FakeTimeProvider(Now));

    [Test]
    public async Task GetToken_LogsInAndCachesTokenUntilNearExpiry()
    {
        var handler = new StubHandler(_ => LoginOk("tok-1", Now.AddMinutes(30)));
        var provider = CreateProvider(handler);

        var first = await provider.GetTokenAsync(CancellationToken.None);
        var second = await provider.GetTokenAsync(CancellationToken.None);

        await Assert.That(first).IsEqualTo("tok-1");
        await Assert.That(second).IsEqualTo("tok-1");
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
    }

    [Test]
    public async Task GetToken_WithoutConfiguredPassword_ReturnsNullWithoutCallingIdentity()
    {
        var handler = new StubHandler(_ => LoginOk("x", Now.AddMinutes(30)));

        var token = await CreateProvider(handler, password: null).GetTokenAsync(CancellationToken.None);

        await Assert.That(token).IsNull();
        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task GetToken_WhenLoginRejected_ReturnsNullInsteadOfThrowing()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var token = await CreateProvider(handler).GetTokenAsync(CancellationToken.None);

        await Assert.That(token).IsNull();
    }

    private static async Task<HttpRequestMessage> SendThroughHandler(
        IHttpContextAccessor accessor, IServiceAccountTokenProvider? provider)
    {
        var terminal = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new AuthHeaderForwardingHandler(accessor, provider) { InnerHandler = terminal };
        using var invoker = new HttpMessageInvoker(handler);
        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://x/"), CancellationToken.None);
        return terminal.Requests.Single();
    }

    [Test]
    public async Task Handler_WithoutIncomingRequest_UsesServiceAccountToken()
    {
        var handler = new StubHandler(_ => LoginOk("svc-token", Now.AddMinutes(30)));
        var request = await SendThroughHandler(new HttpContextAccessor(), CreateProvider(handler));

        await Assert.That(request.Headers.Authorization?.ToString()).IsEqualTo("Bearer svc-token");
    }

    [Test]
    public async Task Handler_WithIncomingAuthorizationHeader_ForwardsUserTokenInsteadOfServiceAccount()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer user-token";
        var stub = new StubHandler(_ => LoginOk("svc-token", Now.AddMinutes(30)));

        var request = await SendThroughHandler(new HttpContextAccessor { HttpContext = context }, CreateProvider(stub));

        await Assert.That(request.Headers.Authorization?.ToString()).IsEqualTo("Bearer user-token");
        await Assert.That(stub.Requests).IsEmpty();
    }
}
