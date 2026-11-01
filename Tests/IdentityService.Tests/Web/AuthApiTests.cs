using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityService.Web.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace IdentityService.Tests.Web;

public class AuthApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task PostLogin_WithValidAdminCredentials_ReturnsTokenWithAdminRole()
    {
        using var factory = new IdentityServiceWebFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(IdentityServiceWebFactory.AdminEmail, IdentityServiceWebFactory.AdminPassword));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Role).IsEqualTo("Admin");
        await Assert.That(body.AccessToken).IsNotNullOrEmpty();
    }

    [Test]
    public async Task PostLogin_WithWrongPassword_ReturnsUnauthorizedWithoutLeakingReason()
    {
        using var factory = new IdentityServiceWebFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(IdentityServiceWebFactory.AdminEmail, "wrong-password"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task PostLogin_WithUnknownEmail_ReturnsUnauthorizedSameAsWrongPassword()
    {
        using var factory = new IdentityServiceWebFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("unknown@crm.local", "whatever"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task PostLogin_WithValidEmployeeCredentials_ReturnsTokenWithEmployeeRoleAndOwnUserId()
    {
        using var factory = new IdentityServiceWebFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(IdentityServiceWebFactory.EmployeeEmail, IdentityServiceWebFactory.EmployeePassword));

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);

        await Assert.That(body!.Role).IsEqualTo("Employee");
        await Assert.That(Guid.TryParse(body.UserId, out _)).IsTrue();
    }

    /// <summary>
    /// Regressionstest fuer Tests/E2E/README.md, Bug 2: der deterministisch geseedete UserId des
    /// Test-Login-Nutzers "employee@crm.local" (MD5-Hash der lowercased E-Mail, siehe
    /// InMemorySeedUserRepository.DeterministicIdFor) MUSS exakt der Id entsprechen, mit der
    /// employee-service seinen dev-only Employee-Datensatz seedet
    /// (siehe EmployeeService.Infrastructure.Seed.DevEmployeeSeedOptions.EmployeeUserId).
    /// Nur wenn beide Werte uebereinstimmen, greift der Ownership-Check (JWT NameIdentifier ==
    /// EmployeeId) fuer diesen Testnutzer end-to-end.
    /// </summary>
    [Test]
    public async Task PostLogin_WithValidEmployeeCredentials_ReturnsUserIdMatchingEmployeeServiceDevSeed()
    {
        using var factory = new IdentityServiceWebFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(IdentityServiceWebFactory.EmployeeEmail, IdentityServiceWebFactory.EmployeePassword));

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);

        // Muss mit EmployeeService.Infrastructure.Seed.DevEmployeeSeedOptions.EmployeeUserId
        // (Default) uebereinstimmen - siehe dortige Dokumentation.
        const string expectedEmployeeUserId = "e573eed0-8c40-749f-f05c-aa6839c81cb4";

        await Assert.That(body!.UserId).IsEqualTo(expectedEmployeeUserId);
    }

    /// <summary>
    /// Regressionstest fuer die konfigurierbare Rate-Limit-Grenze (Tests/E2E/README.md,
    /// "Rate-Limiting-Hinweis"): "RateLimiting:Login:PermitLimit" MUSS tatsaechlich gebunden und
    /// angewendet werden, damit CI/E2E-Laeufe das produktive 5/Minute-Limit ueber den
    /// Aspire-Parameter "login-rate-limit-permit-limit" lockern koennen, ohne appsettings.json zu
    /// aendern. Nutzt absichtlich ein kleines Limit (2) statt des Produktions-Defaults (5), damit
    /// der Test mit wenigen Requests deterministisch und schnell bleibt.
    /// </summary>
    [Test]
    public async Task PostLogin_ExceedsConfiguredPermitLimit_ReturnsTooManyRequests()
    {
        using var factory = new IdentityServiceWebFactory(configureRateLimit: config =>
        {
            config["RateLimiting:Login:PermitLimit"] = "2";
            config["RateLimiting:Login:WindowSeconds"] = "60";
        });
        using var client = factory.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
        {
            last = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(IdentityServiceWebFactory.AdminEmail, "wrong-password"));
        }

        await Assert.That(last!.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
    }
}
