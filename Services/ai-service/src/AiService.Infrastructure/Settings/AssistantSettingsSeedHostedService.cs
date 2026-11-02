using AiService.Application.Abstractions;
using AiService.Domain.Settings;
using AiService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiService.Infrastructure.Settings;

/// <summary>
/// Legt beim ersten Start die Singleton-Zeile <c>assistant_settings</c> an und gleicht sie bei jedem
/// weiteren Start mit den tatsaechlich aktiven Config-Werten ab (docs/adr/0004-ki-assistent-einstellungen-laufzeit.md,
/// Abschnitt "Persistenz"). Faengt Fehler analog <see cref="Persistence.DatabaseMigrationHostedService{TDbContext}"/>
/// ab, damit Tests ohne echte Datenbank nicht am Start scheitern.
/// </summary>
public sealed class AssistantSettingsSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<AssistantSettingsSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<AiDbContext>();
            if (dbContext is not null)
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
            }

            var repository = scope.ServiceProvider.GetRequiredService<IAssistantSettingsRepository>();

            var provider = AssistantRuntimeConfig.ResolveProvider(configuration);
            var model = AssistantRuntimeConfig.ResolveModel(configuration, provider);
            var qdrantBaseUrl = AssistantRuntimeConfig.ResolveQdrantBaseUrl(configuration);
            var now = timeProvider.GetUtcNow();

            var existing = await repository.GetAsync(cancellationToken);
            if (existing is null)
            {
                await repository.AddAsync(AssistantSettings.SeedFromStartupConfig(provider, model, qdrantBaseUrl, now), cancellationToken);
            }
            else
            {
                existing.ReconcileWithStartupConfig(provider, model, qdrantBaseUrl, now);
            }

            await repository.SaveChangesAsync(cancellationToken);

            logger.LogInformation("assistant_settings beim Start erfolgreich initialisiert/abgeglichen.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "assistant_settings-Initialisierung beim Start fehlgeschlagen (z.B. keine echte Datenbank in Tests) - " +
                "Service startet trotzdem weiter.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
