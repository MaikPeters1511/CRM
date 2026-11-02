using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IdentityService.Tests.Web;

/// <summary>
/// Seit docs/adr/0004 (identity_db) benoetigt identity-service zwingend eine echte PostgreSQL-
/// Datenbank - identisches Muster wie EmployeeServiceRealInfrastructureWebFactory
/// (Tests/EmployeeService.Tests/Web): <c>AddIdentityServiceInfrastructure</c> liest den
/// Connection-String EAGER (vor builder.Build()), daher wird hier die Prozess-Umgebungsvariable
/// <c>ConnectionStrings__identity_db</c> gesetzt statt WebApplicationFactory's
/// ConfigureAppConfiguration-Hook zu nutzen (der erst bei Build() greift). Tests, die diese
/// Factory verwenden, sind deshalb <c>[NotInParallel]</c> (globale Prozess-Umgebungsvariable).
/// </summary>
public sealed class IdentityServiceWebFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@crm.local";
    public const string AdminPassword = "Test-Admin-Passw0rd!";
    public const string EmployeeEmail = "employee@crm.local";
    public const string EmployeePassword = "Test-Employee-Passw0rd!";

    private const string ConnectionStringEnvVarName = "ConnectionStrings__identity_db";

    private readonly Action<Dictionary<string, string?>>? _configureRateLimit;
    private readonly string? _previousConnectionStringEnvVar;

    public IdentityServiceWebFactory(string connectionString, Action<Dictionary<string, string?>>? configureRateLimit = null)
    {
        _configureRateLimit = configureRateLimit;
        _previousConnectionStringEnvVar = Environment.GetEnvironmentVariable(ConnectionStringEnvVarName);
        Environment.SetEnvironmentVariable(ConnectionStringEnvVarName, connectionString);
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Environment.SetEnvironmentVariable(ConnectionStringEnvVarName, _previousConnectionStringEnvVar);
    }
}
