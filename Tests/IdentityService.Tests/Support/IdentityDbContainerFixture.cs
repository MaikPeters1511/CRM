using IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace IdentityService.Tests.Support;

/// <summary>
/// Startet einen echten PostgreSQL-Container (Testcontainers) und wendet die tatsaechlichen
/// EF-Core-Migrationen von <see cref="IdentityDbContext"/> darauf an - identisches Muster wie
/// EmployeeDbContainerFixture (Tests/EmployeeService.Tests/Support), docs/adr/0004 Abschnitt 1.
/// Seit identity-mfa-totp.openspec.md ist identity_db eine notwendige Abhaengigkeit von
/// identity-service; Tests laufen daher grundsaetzlich gegen echte Infrastruktur statt eines Fakes.
/// </summary>
public sealed class IdentityDbContainerFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("identity_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new IdentityDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
