using EmployeeService.Application.Abstractions;
using EmployeeService.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeService.Tests.Web;

public sealed class EmployeeServiceWebFactory : WebApplicationFactory<Program>
{
    public FakeEmployeeRepository Repository { get; } = new();

    public FakeAppointmentAvailabilityClient AppointmentAvailabilityClient { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:employee_db"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IEmployeeRepository>(Repository);
            services.AddSingleton<IAppointmentAvailabilityClient>(AppointmentAvailabilityClient);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
