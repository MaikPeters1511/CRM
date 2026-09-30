using AppointmentService.Application.Abstractions;
using AppointmentService.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppointmentService.Tests.Web;

/// <summary>
/// Gegenstueck zu <see cref="AppointmentServiceWebFactory"/>, das bewusst KEIN Fake-Repository
/// einsetzt: <c>IAppointmentRepository</c> bleibt die produktionsechte
/// <c>EfAppointmentRepository -&gt; AppointmentDbContext</c>-Registrierung, nur der
/// Connection-String zeigt auf einen echten, migrierten Testcontainers-PostgreSQL (siehe
/// <see cref="AppointmentDbContainerFixture"/> in Tests/AppointmentService.Tests/Support).
/// <c>ICustomerVerificationClient</c>/<c>IEmployeeAvailabilityClient</c> bleiben Fakes - das sind
/// externe HTTP-Clients zu anderen Services, nicht Teil des hier untersuchten
/// Wolverine/EF-Core-Bugs (Tests/E2E/README.md, Bug 4).
///
/// Wichtig: der Connection-String wird EAGER (nicht lazy) in
/// <c>AppointmentService.Infrastructure.DependencyInjection.AddAppointmentServiceInfrastructure</c>
/// gelesen - siehe die ausfuehrliche Begruendung in EmployeeServiceRealInfrastructureWebFactory
/// (employee-service), analoges Problem/Loesung hier. Deshalb wird die tatsaechliche
/// Prozess-Umgebungsvariable <c>ConnectionStrings__appointment_db</c> gesetzt statt nur
/// <c>ConfigureAppConfiguration</c> zu verwenden; der zugehoerige Test ist deshalb
/// <c>[NotInParallel]</c>.
/// </summary>
public sealed class AppointmentServiceRealInfrastructureWebFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringEnvVarName = "ConnectionStrings__appointment_db";

    private readonly string? _previousConnectionStringEnvVar;

    public FakeCustomerVerificationClient CustomerVerificationClient { get; } = new();

    public FakeEmployeeAvailabilityClient EmployeeAvailabilityClient { get; } = new();

    public AppointmentServiceRealInfrastructureWebFactory(string connectionString)
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
            services.AddSingleton<ICustomerVerificationClient>(CustomerVerificationClient);
            services.AddSingleton<IEmployeeAvailabilityClient>(EmployeeAvailabilityClient);

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
