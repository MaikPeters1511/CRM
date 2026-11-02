namespace AiService.Application.Settings;

/// <summary>
/// Antwort-DTO fuer <c>GET</c>/<c>PUT /api/assistant/settings</c>
/// (ki-einstellungen-backend.openspec.md, Abschnitt 3).
/// </summary>
public sealed record AssistantSettingsDto(
    bool Enabled,
    string Provider,
    string Model,
    string QdrantBaseUrl,
    bool ApiKeyConfigured,
    string? MaskedApiKey,
    bool RestartRequired,
    DateTimeOffset? LastIndexSyncAt,
    string ApiKeyManagementNote);
