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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = "https://crm-identity-test",
                ["Authentication:JwtBearer:Audience"] = "crm-api-test",
                ["Authentication:JwtBearer:SigningKey"] = "test-only-signing-key-not-for-production-012345",
                ["IdentitySeed:AdminEmail"] = AdminEmail,
                ["IdentitySeed:AdminPassword"] = AdminPassword,
                ["IdentitySeed:EmployeeEmail"] = EmployeeEmail,
                ["IdentitySeed:EmployeePassword"] = EmployeePassword,
            });
        });
    }
}
