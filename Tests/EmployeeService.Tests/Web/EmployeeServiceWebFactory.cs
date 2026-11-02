using EmployeeService.Application.Abstractions;
using EmployeeService.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeService.Tests.Web;

public sealed class EmployeeServiceWebFactory : WebApplicationFactory<Program>
{
    public FakeEmployeeRepository Repository { get; } = new();

    public FakeAppointmentAvailabilityClient AppointmentAvailabilityClient { get; } = new();

    public FakeNotificationRepository NotificationRepository { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // Bewusst NICHT "Development": Integrationstests ersetzen IEmployeeRepository durch
        // FakeEmployeeRepository und pruefen exakte Count()-Erwartungen. Waere die Umgebung
        // "Development", wuerde DevEmployeeSeedHostedService (dev-/test-only, siehe
        // EmployeeService.Infrastructure.Seed) zusaetzlich einen Employee seeden und diese
        // Erwartungen verfaelschen. "Testing" ist ein eigenstaendiger ASP.NET-Core-Umgebungsname,
        // der von IsDevelopment()-Checks nicht erfasst wird.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:employee_db"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                // Nur fuer Tests: Program.cs validiert beim Start, dass ein SigningKey konfiguriert ist.
                // Die tatsaechliche Authentifizierung in Tests laeuft ueber TestAuthHandler, nicht JwtBearer.
                ["Authentication:JwtBearer:Issuer"] = "https://crm-identity-test",
                ["Authentication:JwtBearer:Audience"] = "crm-api-test",
                ["Authentication:JwtBearer:SigningKey"] = "test-only-signing-key-not-for-production-012345",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IEmployeeRepository>(Repository);
            services.AddSingleton<IAppointmentAvailabilityClient>(AppointmentAvailabilityClient);
            services.AddSingleton<INotificationRepository>(NotificationRepository);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
