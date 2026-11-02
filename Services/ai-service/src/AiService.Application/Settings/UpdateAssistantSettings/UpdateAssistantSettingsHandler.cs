using AiService.Application.Abstractions;
using AiService.Application.Settings.GetAssistantSettings;

namespace AiService.Application.Settings.UpdateAssistantSettings;

public static class UpdateAssistantSettingsHandler
{
    public static async Task<AssistantSettingsDto> Handle(
        UpdateAssistantSettingsCommand command,
        IAssistantSettingsRepository settingsRepository,
        IAssistantApiKeyInspector apiKeyInspector,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "assistant_settings ist nicht initialisiert - AssistantSettingsSeedHostedService ist beim Start fehlgeschlagen.");

        settings.ApplyAdminUpdate(command.Enabled, command.Provider, command.Model, command.QdrantBaseUrl, timeProvider.GetUtcNow());
        await settingsRepository.SaveChangesAsync(cancellationToken);

        return new AssistantSettingsDto(
            settings.Enabled,
            settings.Provider,
            settings.Model,
            settings.QdrantBaseUrl,
            apiKeyInspector.IsConfigured(settings.Provider),
            apiKeyInspector.GetMasked(settings.Provider),
            settings.RestartRequired,
            settings.LastIndexSyncAt,
            AssistantSettingsMessages.ApiKeyManagementNote);
    }
}
