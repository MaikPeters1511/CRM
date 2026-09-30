using CustomerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace CustomerService.Tests.Support;

/// <summary>
/// Startet einen echten PostgreSQL-Container (Testcontainers) und wendet die tatsaechlichen
/// EF-Core-Migrationen von <see cref="CustomerDbContext"/> darauf an.
///
/// Zweck (Tests/E2E/README.md, Bug 4): die bestehenden Integrationstests
/// (<see cref="CustomerServiceWebFactory"/>) ersetzen <c>ICustomerRepository</c> durch ein
/// abhaengigkeitsfreies Fake und durchlaufen damit NICHT die DI-Kette
/// (<c>ICustomerRepository -&gt; EfCustomerRepository -&gt; CustomerDbContext -&gt;
/// DbContextOptions&lt;CustomerDbContext&gt;</c>), die den scoped-Service-Location-Bug in Wolverine
/// ausgeloest hat (analog zu employee-service). Diese Fixture stellt eine echte, migrierte
/// Datenbank bereit, damit ein Regressionstest die echte Infrastruktur (kein Fake) gegen den
/// kompletten Produktionspfad verifizieren kann.
/// </summary>
public sealed class CustomerDbContainerFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("customer_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new CustomerDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
