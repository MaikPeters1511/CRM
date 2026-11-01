using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiService.Infrastructure.Persistence;

/// <summary>
/// Wendet beim Service-Start automatisch alle ausstehenden EF-Core-Migrationen fuer
/// <typeparamref name="TDbContext"/> an. Identisches Muster wie
/// <c>CatalogService.Infrastructure.Persistence.DatabaseMigrationHostedService</c>.
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
