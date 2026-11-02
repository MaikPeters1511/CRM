using AppointmentService.Application.Abstractions;
using AppointmentService.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppointmentService.Tests.Web;

public sealed class AppointmentServiceWebFactory : WebApplicationFactory<Program>
{
    public FakeAppointmentRepository Repository { get; } = new();
    public FakeCustomerVerificationClient CustomerVerificationClient { get; } = new();
    public FakeEmployeeAvailabilityClient EmployeeAvailabilityClient { get; } = new();
    public FakeEmployeeAbsenceClient EmployeeAbsenceClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:appointment_db"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                // Nur fuer Tests: Program.cs validiert beim Start, dass ein SigningKey konfiguriert ist.
                // Die tatsaechliche Authentifizierung in Tests laeuft ueber TestAuthHandler, nicht JwtBearer.
                ["Authentication:JwtBearer:Issuer"] = "https://crm-identity-test",
                ["Authentication:JwtBearer:Audience"] = "crm-api-test",
                ["Authentication:JwtBearer:SigningKey"] = "test-only-signing-key-not-for-production-012345",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAppointmentRepository>(Repository);
            services.AddSingleton<ICustomerVerificationClient>(CustomerVerificationClient);
            services.AddSingleton<IEmployeeAvailabilityClient>(EmployeeAvailabilityClient);
            services.AddSingleton<IEmployeeAbsenceClient>(EmployeeAbsenceClient);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
