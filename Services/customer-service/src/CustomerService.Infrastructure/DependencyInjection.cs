using CustomerService.Application.Abstractions;
using CustomerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomerServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("customer_db")
            ?? Environment.GetEnvironmentVariable("CUSTOMER_DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "No connection string configured for 'customer_db'. Set ConnectionStrings:customer_db or CUSTOMER_DB_CONNECTION_STRING.");

        services.AddDbContext<CustomerDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<ICustomerRepository, EfCustomerRepository>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
