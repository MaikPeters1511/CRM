using CatalogService.Application.Abstractions;
using CatalogService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CatalogService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("catalog_db")
            ?? Environment.GetEnvironmentVariable("CATALOG_DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "No connection string configured for 'catalog_db'. Set ConnectionStrings:catalog_db or CATALOG_DB_CONNECTION_STRING.");

        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IServiceRepository, EfServiceRepository>();
        services.AddSingleton(TimeProvider.System);

        // Tests/E2E/README.md, Bug 5: wendet beim Start automatisch alle ausstehenden Migrationen
        // an (jede Umgebung, nicht nur Development - kein separater Migrations-Deployment-Schritt
        // in dieser Landschaft vorhanden).
        services.AddHostedService<DatabaseMigrationHostedService<CatalogDbContext>>();

        return services;
    }
}
