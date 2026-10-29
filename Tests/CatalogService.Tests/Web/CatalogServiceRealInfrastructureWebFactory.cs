using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CatalogService.Tests.Web;

/// <summary>
/// Gegenstueck zu <see cref="CatalogServiceWebFactory"/>, das bewusst KEIN Fake-Repository
/// einsetzt: <c>IServiceRepository</c> bleibt die produktionsechte
/// <c>EfServiceRepository -&gt; CatalogDbContext</c>-Registrierung, nur der Connection-String
/// zeigt auf einen echten, migrierten Testcontainers-PostgreSQL (siehe
/// <see cref="CatalogDbContainerFixture"/> in Tests/CatalogService.Tests/Support, analog
/// CustomerServiceRealInfrastructureWebFactory).
///
/// Wichtig: der Connection-String wird EAGER (nicht lazy) in
/// <c>CatalogService.Infrastructure.DependencyInjection.AddCatalogServiceInfrastructure</c>
/// gelesen, daher wird die tatsaechliche Prozess-Umgebungsvariable
/// <c>ConnectionStrings__catalog_db</c> gesetzt statt nur <c>ConfigureAppConfiguration</c> zu
/// verwenden; der zugehoerige Test ist deshalb <c>[NotInParallel]</c>.
/// </summary>
public sealed class CatalogServiceRealInfrastructureWebFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringEnvVarName = "ConnectionStrings__catalog_db";

    private readonly string? _previousConnectionStringEnvVar;

    public CatalogServiceRealInfrastructureWebFactory(string connectionString)
    {
        _previousConnectionStringEnvVar = Environment.GetEnvironmentVariable(ConnectionStringEnvVarName);
        Environment.SetEnvironmentVariable(ConnectionStringEnvVarName, connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = "https://crm-identity-test",
                ["Authentication:JwtBearer:Audience"] = "crm-api-test",
                ["Authentication:JwtBearer:SigningKey"] = "test-only-signing-key-not-for-production-012345",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Environment.SetEnvironmentVariable(ConnectionStringEnvVarName, _previousConnectionStringEnvVar);
    }
}
