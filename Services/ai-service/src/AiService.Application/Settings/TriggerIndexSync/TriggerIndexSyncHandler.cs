using AiService.Application.Abstractions;
using AiService.Application.IndexSync;

namespace AiService.Application.Settings.TriggerIndexSync;

public static class TriggerIndexSyncHandler
{
    public static async Task<TriggerIndexSyncResult> Handle(
        TriggerIndexSyncCommand command,
        IAssistantSettingsRepository settingsRepository,
        ICatalogQueryClient catalogClient,
        IEmployeeQueryClient employeeClient,
        IAvailabilityIndexClient indexClient,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "assistant_settings ist nicht initialisiert - AssistantSettingsSeedHostedService ist beim Start fehlgeschlagen.");

        var previousSyncAt = settings.LastIndexSyncAt;

        await IndexConsistencySyncHandler.Handle(
            new RunIndexConsistencySyncCommand(), catalogClient, employeeClient, indexClient,
            settingsRepository, timeProvider, cancellationToken);

        return new TriggerIndexSyncResult(previousSyncAt, settings.LastIndexSyncAt ?? timeProvider.GetUtcNow());
    }
}
