namespace AiService.Application.Abstractions;

/// <summary>
/// Liest ausschliesslich, OB fuer den angegebenen Provider ein API-Key konfiguriert ist, und liefert
/// eine maskierte Darstellung (z.B. <c>sk-••••1234</c>) - NIEMALS den Klartext-Key (Requirement
/// "API-Key wird niemals im Klartext zurückgegeben", ki-einstellungen-backend.openspec.md).
/// </summary>
public interface IAssistantApiKeyInspector
{
    bool IsConfigured(string provider);

    string? GetMasked(string provider);
}
