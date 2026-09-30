using EmployeeService.Application.Abstractions;
using EmployeeService.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeService.Tests.Web;

/// <summary>
/// Dedizierte Factory NUR fuer den Test des dev-/test-only <c>DevEmployeeSeedHostedService</c>
/// (siehe Tests/E2E/README.md, Bug 2). Im Unterschied zu <see cref="EmployeeServiceWebFactory"/>
/// laeuft diese Factory bewusst in "Development", um die Seed-Logik zu triggern.
/// </summary>
public sealed class EmployeeServiceDevSeedWebFactory : WebApplicationFactory<Program>
{
    public FakeEmployeeRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:employee_db"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Authentication:JwtBearer:Issuer"] = "https://crm-identity-test",
                ["Authentication:JwtBearer:Audience"] = "crm-api-test",
                ["Authentication:JwtBearer:SigningKey"] = "test-only-signing-key-not-for-production-012345",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IEmployeeRepository>(Repository);
            services.AddSingleton<IAppointmentAvailabilityClient>(new FakeAppointmentAvailabilityClient());

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
