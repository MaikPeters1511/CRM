using CatalogService.Application.Abstractions;
using CatalogService.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CatalogService.Tests.Web;

public sealed class CatalogServiceWebFactory : WebApplicationFactory<Program>
{
    public FakeServiceRepository Repository { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:catalog_db"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                // Nur fuer Tests: Program.cs validiert beim Start, dass ein SigningKey konfiguriert ist.
                // Die tatsaechliche Authentifizierung in Tests laeuft ueber TestAuthHandler, nicht JwtBearer.
                ["Authentication:JwtBearer:Issuer"] = "https://crm-identity-test",
                ["Authentication:JwtBearer:Audience"] = "crm-api-test",
                ["Authentication:JwtBearer:SigningKey"] = "test-only-signing-key-not-for-production-012345",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IServiceRepository>(Repository);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
