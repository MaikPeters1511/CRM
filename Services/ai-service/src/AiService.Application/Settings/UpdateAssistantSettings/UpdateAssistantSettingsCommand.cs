namespace AiService.Application.Settings.UpdateAssistantSettings;

/// <summary>
/// <c>PUT /api/assistant/settings</c> - Admin-only. Nimmt bewusst KEIN API-Key-Feld entgegen
/// (ki-einstellungen-backend.openspec.md, Requirement "API-Key wird niemals im Klartext zurückgegeben").
/// </summary>
public sealed record UpdateAssistantSettingsCommand(
    bool Enabled, string Provider, string Model, string QdrantBaseUrl);
