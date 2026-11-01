using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IdentityService.Tests.Web;

public sealed class IdentityServiceWebFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@crm.local";
    public const string AdminPassword = "Test-Admin-Passw0rd!";
    public const string EmployeeEmail = "employee@crm.local";
    public const string EmployeePassword = "Test-Employee-Passw0rd!";

    private readonly Action<Dictionary<string, string?>>? _configureRateLimit;

    public IdentityServiceWebFactory(Action<Dictionary<string, string?>>? configureRateLimit = null)
    {
        _configureRateLimit = configureRateLimit;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = "https://crm-identity-test",
                ["Authentication:JwtBearer:Audience"] = "crm-api-test",
                ["Authentication:JwtBearer:SigningKey"] = "test-only-signing-key-not-for-production-012345",
                ["IdentitySeed:AdminEmail"] = AdminEmail,
                ["IdentitySeed:AdminPassword"] = AdminPassword,
                ["IdentitySeed:EmployeeEmail"] = EmployeeEmail,
                ["IdentitySeed:EmployeePassword"] = EmployeePassword,
            };
            _configureRateLimit?.Invoke(settings);
            configBuilder.AddInMemoryCollection(settings);
        });
    }
}
