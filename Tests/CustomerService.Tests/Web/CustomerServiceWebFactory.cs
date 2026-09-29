using CustomerService.Application.Abstractions;
using CustomerService.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerService.Tests.Web;

public sealed class CustomerServiceWebFactory : WebApplicationFactory<Program>
{
    public FakeCustomerRepository Repository { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:customer_db"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                // Nur fuer Tests: Program.cs validiert beim Start, dass ein SigningKey konfiguriert ist.
                // Die tatsaechliche Authentifizierung in Tests laeuft ueber TestAuthHandler, nicht JwtBearer.
                ["Authentication:JwtBearer:Issuer"] = "https://crm-identity-test",
                ["Authentication:JwtBearer:Audience"] = "crm-api-test",
                ["Authentication:JwtBearer:SigningKey"] = "test-only-signing-key-not-for-production-012345",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ICustomerRepository>(Repository);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
