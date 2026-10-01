using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EmployeeService.Infrastructure.Persistence;

/// <summary>
/// Wendet beim Service-Start automatisch alle ausstehenden EF-Core-Migrationen fuer
/// <typeparamref name="TDbContext"/> an (Tests/E2E/README.md, Bug 5). Laeuft in JEDER Umgebung
/// (nicht nur Development) - in dieser Microservices-Landschaft gibt es keinen separaten
/// Migrations-Deployment-Schritt, ohne diesen automatischen Schritt scheitert sonst jeder Request
/// gegen eine frische/leere PostgreSQL-Instanz mit 500. Bewusst getrennt vom reinen
/// Development-only Daten-Seeding (siehe <c>EmployeeService.Infrastructure.Seed.DevEmployeeSeedHostedService</c>,
/// Bug 2).
///
/// Fehler hier duerfen den Service-Start dennoch NIEMALS verhindern (z.B. Integrationstests mit
/// Fake-Repository und einem nicht existierenden/ungueltigen Connection-String, die den
/// DbContext selbst nie tatsaechlich abfragen - siehe EmployeeServiceWebFactory).
/// </summary>
public sealed class DatabaseMigrationHostedService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrationHostedService<TDbContext>> logger) : IHostedService
    where TDbContext : DbContext
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);

            logger.LogInformation(
                "{DbContext}: ausstehende EF-Core-Migrationen beim Start erfolgreich angewendet.",
                typeof(TDbContext).Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "{DbContext}: Migration beim Start fehlgeschlagen (z.B. keine echte Datenbank in Tests) - " +
                "Service startet trotzdem weiter.",
                typeof(TDbContext).Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
