using AiService.Application.Abstractions;
using AiService.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiService.Tests.Web;

public sealed class AiServiceWebFactory : WebApplicationFactory<Program>
{
    public FakeChatSessionRepository SessionRepository { get; } = new();
    public FakeCatalogQueryClient CatalogClient { get; } = new();
    public FakeEmployeeQueryClient EmployeeClient { get; } = new();
    public FakeAppointmentQueryClient AppointmentClient { get; } = new();
    public FakeCustomerQueryClient CustomerClient { get; } = new();
    public FakeAvailabilityIndexClient IndexClient { get; } = new();
    public FakeLlmClient LlmClient { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ai_db"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Authentication:JwtBearer:Issuer"] = "https://crm-identity-test",
                ["Authentication:JwtBearer:Audience"] = "crm-api-test",
                ["Authentication:JwtBearer:SigningKey"] = "test-only-signing-key-not-for-production-012345",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IChatSessionRepository>(SessionRepository);
            services.AddSingleton<ICatalogQueryClient>(CatalogClient);
            services.AddSingleton<IEmployeeQueryClient>(EmployeeClient);
            services.AddSingleton<IAppointmentQueryClient>(AppointmentClient);
            services.AddSingleton<ICustomerQueryClient>(CustomerClient);
            services.AddSingleton<IAvailabilityIndexClient>(IndexClient);
            services.AddSingleton<ILlmClient>(LlmClient);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
