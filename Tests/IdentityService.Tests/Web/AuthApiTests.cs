using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityService.Web.Controllers;

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
}
