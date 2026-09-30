using EmployeeService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace EmployeeService.Tests.Support;

/// <summary>
/// Startet einen echten PostgreSQL-Container (Testcontainers) und wendet die tatsaechlichen
/// EF-Core-Migrationen von <see cref="EmployeeDbContext"/> darauf an.
///
/// Zweck (Tests/E2E/README.md, Bug 4): die bestehenden Integrationstests
/// (<see cref="EmployeeServiceWebFactory"/>) ersetzen <c>IEmployeeRepository</c> durch ein
/// abhaengigkeitsfreies Fake und durchlaufen damit NICHT die DI-Kette
/// (<c>IEmployeeRepository -&gt; EfEmployeeRepository -&gt; EmployeeDbContext -&gt;
/// DbContextOptions&lt;EmployeeDbContext&gt;</c>), die den scoped-Service-Location-Bug in Wolverine
/// ausgeloest hat. Diese Fixture stellt eine echte, migrierte Datenbank bereit, damit ein
/// Regressionstest die echte Infrastruktur (kein Fake) gegen den kompletten Produktionspfad
/// verifizieren kann. Wird ueber <c>[ClassDataSource&lt;T&gt;(Shared = SharedType.PerTestSession)]</c>
/// einmal pro Testsession geteilt, da der Container-Start teuer ist.
/// </summary>
public sealed class EmployeeDbContainerFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("employee_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<EmployeeDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new EmployeeDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
