using AiService.Application.Abstractions;
using AiService.Application.IndexSync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiService.Infrastructure.IndexSync;

/// <summary>
/// Taeglicher, niedrigpriorer Konsistenz-Sync (docs/adr/0003-ai-service-qdrant-index-strategie.md,
/// Abschnitt 3/5) - laeuft einmal direkt beim Start (Initial-Sync, derselbe Code-Pfad) und danach
/// periodisch alle 24 Stunden. KNOWN SIMPLIFICATION (analog AppointmentReminderHostedService): simple
/// Polling-Hosted-Service statt Cron/Quartz - fuer einen taeglichen Reparatur-Job ausreichend.
/// </summary>
public sealed class DailyIndexConsistencySyncHostedService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<DailyIndexConsistencySyncHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var catalogClient = scope.ServiceProvider.GetRequiredService<ICatalogQueryClient>();
                var employeeClient = scope.ServiceProvider.GetRequiredService<IEmployeeQueryClient>();
                var indexClient = scope.ServiceProvider.GetRequiredService<IAvailabilityIndexClient>();

                await IndexConsistencySyncHandler.Handle(
                    new RunIndexConsistencySyncCommand(), catalogClient, employeeClient, indexClient, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Qdrant-Konsistenz-Sync fehlgeschlagen - naechster Versuch in {Interval}.", SyncInterval);
            }

            try
            {
                await Task.Delay(SyncInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested - exit the loop.
            }
        }
    }
}
