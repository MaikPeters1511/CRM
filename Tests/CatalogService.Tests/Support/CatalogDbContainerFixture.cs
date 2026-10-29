using CatalogService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace CatalogService.Tests.Support;

/// <summary>
/// Startet einen echten PostgreSQL-Container (Testcontainers) und wendet die tatsaechlichen
/// EF-Core-Migrationen von <see cref="CatalogDbContext"/> darauf an (analog
/// CustomerDbContainerFixture; Tests/E2E/README.md, Bug 4).
/// </summary>
public sealed class CatalogDbContainerFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("catalog_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new CatalogDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
