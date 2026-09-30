using AppointmentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace AppointmentService.Tests.Support;

/// <summary>
/// Startet einen echten PostgreSQL-Container (Testcontainers) und wendet die tatsaechlichen
/// EF-Core-Migrationen von <see cref="AppointmentDbContext"/> darauf an.
///
/// Zweck (Tests/E2E/README.md, Bug 4): die bestehenden Integrationstests
/// (<see cref="AppointmentServiceWebFactory"/>) ersetzen <c>IAppointmentRepository</c> durch ein
/// abhaengigkeitsfreies Fake und durchlaufen damit NICHT die DI-Kette
/// (<c>IAppointmentRepository -&gt; EfAppointmentRepository -&gt; AppointmentDbContext -&gt;
/// DbContextOptions&lt;AppointmentDbContext&gt;</c>), die den scoped-Service-Location-Bug in
/// Wolverine ausgeloest hat (analog zu employee-service/customer-service). Diese Fixture stellt
/// eine echte, migrierte Datenbank bereit, damit ein Regressionstest die echte Infrastruktur
/// (kein Fake) gegen den kompletten Produktionspfad verifizieren kann.
/// </summary>
public sealed class AppointmentDbContainerFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("appointment_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppointmentDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new AppointmentDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
