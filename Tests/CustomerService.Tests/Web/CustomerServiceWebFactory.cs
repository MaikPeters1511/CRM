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
