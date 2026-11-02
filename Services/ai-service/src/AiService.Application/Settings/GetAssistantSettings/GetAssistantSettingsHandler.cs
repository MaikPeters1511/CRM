using AiService.Application.Abstractions;

namespace AiService.Application.Settings.GetAssistantSettings;

public static class GetAssistantSettingsHandler
{
    public static async Task<AssistantSettingsDto> Handle(
        GetAssistantSettingsQuery query,
        IAssistantSettingsRepository settingsRepository,
        IAssistantApiKeyInspector apiKeyInspector,
        CancellationToken cancellationToken)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "assistant_settings ist nicht initialisiert - AssistantSettingsSeedHostedService ist beim Start fehlgeschlagen.");

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

public static class AssistantSettingsMessages
{
    public const string ApiKeyManagementNote =
        "Der API-Key kann ausschließlich über eine Secret/Environment-Variable gesetzt werden " +
        "(z.B. Aspire-Parameter 'openai-api-key') und wird niemals über diese API entgegengenommen " +
        "oder im Klartext zurückgegeben.";

    public const string RestartRequiredNote =
        "Provider, Modell und Qdrant-URL wurden gespeichert, werden aber erst nach einem Neustart " +
        "von ai-service aktiv (siehe docs/adr/0004-ki-assistent-einstellungen-laufzeit.md).";
}
