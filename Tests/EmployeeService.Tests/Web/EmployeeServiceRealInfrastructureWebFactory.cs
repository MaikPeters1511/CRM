using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeService.Tests.Web;

/// <summary>
/// Gegenstueck zu <see cref="EmployeeServiceWebFactory"/>, das bewusst KEIN Fake-Repository
/// einsetzt: <c>IEmployeeRepository</c> bleibt die produktionsechte
/// <c>EfEmployeeRepository -&gt; EmployeeDbContext</c>-Registrierung aus
/// <c>AddEmployeeServiceInfrastructure</c>, nur der Connection-String zeigt auf einen echten,
/// migrierten Testcontainers-PostgreSQL (siehe <see cref="EmployeeDbContainerFixture"/> in
/// Tests/EmployeeService.Tests/Support). Damit durchlaeuft dieser Testpfad exakt dieselbe
/// Wolverine-/EF-Core-DI-Kette wie in Produktion (Tests/E2E/README.md, Bug 4) - im Gegensatz zu
/// <see cref="EmployeeServiceWebFactory"/>, die dies mit einem abhaengigkeitsfreien Fake umgeht.
///
/// Wichtig: <c>EmployeeService.Infrastructure.DependencyInjection.AddEmployeeServiceInfrastructure</c>
/// liest den Connection-String EAGER (nicht lazy) direkt beim Aufruf von
/// <c>builder.Services.AddEmployeeServiceInfrastructure(...)</c> in <c>Program.cs</c> - also VOR
/// <c>builder.Build()</c>. <see cref="WebApplicationFactory{TEntryPoint}"/>s
/// <c>ConfigureAppConfiguration</c>-Hook fuer Minimal-Hosting-Programme greift dagegen erst zum
/// Zeitpunkt von <c>Build()</c> (zu spaet fuer diesen eager Read - "appsettings.json" mit dem
/// leeren Platzhalter <c>"employee_db": ""</c> haette sonst weiterhin Vorrang, da eine leere
/// Zeichenfolge den <c>??</c>-Fallback in <c>DependencyInjection.cs</c> nicht auslöst). Deshalb wird
/// hier die tatsaechliche Prozess-Umgebungsvariable <c>ConnectionStrings__employee_db</c> gesetzt -
/// die von <c>WebApplication.CreateBuilder(args)</c> bereits beim Aufruf selbst (also vor jeglichem
/// nachfolgenden Code in Program.cs) eingelesen wird. Aus demselben Grund ist der Test dieser
/// Factory <c>[NotInParallel]</c> markiert (globale Prozess-Umgebungsvariable).
/// </summary>
public sealed class EmployeeServiceRealInfrastructureWebFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringEnvVarName = "ConnectionStrings__employee_db";

    private readonly string? _previousConnectionStringEnvVar;

    public EmployeeServiceRealInfrastructureWebFactory(string connectionString)
    {
        _previousConnectionStringEnvVar = Environment.GetEnvironmentVariable(ConnectionStringEnvVarName);
        Environment.SetEnvironmentVariable(ConnectionStringEnvVarName, connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Development", damit die Konfiguration identisch zum echten Betrieb bleibt (u.a.
        // DevEmployeeSeedHostedService, der genauso auch beim realen Start seedet) - anders als
        // EmployeeServiceWebFactory ("Testing"), die den Fake-Pfad bewusst seed-frei haelt.
        builder.UseEnvironment("Development");

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
            // Kein Ersetzen von IEmployeeRepository/EmployeeDbContext - genau das ist der Punkt
            // dieser Factory. Nur die Authentifizierung wird wie in EmployeeServiceWebFactory
            // durch den deterministischen TestAuthHandler ersetzt, um echte JWT-Ausstellung
            // (Aufgabe des identity-service) nicht in diesen Test hineinzuziehen.
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
