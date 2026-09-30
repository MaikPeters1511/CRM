using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerService.Tests.Web;

/// <summary>
/// Gegenstueck zu <see cref="CustomerServiceWebFactory"/>, das bewusst KEIN Fake-Repository
/// einsetzt: <c>ICustomerRepository</c> bleibt die produktionsechte
/// <c>EfCustomerRepository -&gt; CustomerDbContext</c>-Registrierung, nur der Connection-String
/// zeigt auf einen echten, migrierten Testcontainers-PostgreSQL (siehe
/// <see cref="CustomerDbContainerFixture"/> in Tests/CustomerService.Tests/Support).
///
/// Wichtig: der Connection-String wird EAGER (nicht lazy) in
/// <c>CustomerService.Infrastructure.DependencyInjection.AddCustomerServiceInfrastructure</c>
/// gelesen - siehe die ausfuehrliche Begruendung in
/// EmployeeServiceRealInfrastructureWebFactory (employee-service), analoges Problem/Loesung hier.
/// Deshalb wird die tatsaechliche Prozess-Umgebungsvariable <c>ConnectionStrings__customer_db</c>
/// gesetzt statt nur <c>ConfigureAppConfiguration</c> zu verwenden; der zugehoerige Test ist
/// deshalb <c>[NotInParallel]</c>.
/// </summary>
public sealed class CustomerServiceRealInfrastructureWebFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringEnvVarName = "ConnectionStrings__customer_db";

    private readonly string? _previousConnectionStringEnvVar;

    public CustomerServiceRealInfrastructureWebFactory(string connectionString)
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
