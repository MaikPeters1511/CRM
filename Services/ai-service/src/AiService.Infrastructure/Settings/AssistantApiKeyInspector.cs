using AiService.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace AiService.Infrastructure.Settings;

/// <summary>
/// Liest ausschliesslich zum Zweck der Maskierung, ob ein API-Key konfiguriert ist - der Klartext-Wert
/// verlaesst diese Klasse nie (Requirement "API-Key wird niemals im Klartext zurückgegeben").
/// LMStudio kennt in diesem Projekt kein API-Key-Konzept (siehe DependencyInjection.AddLlmClient).
/// </summary>
public sealed class AssistantApiKeyInspector(IConfiguration configuration) : IAssistantApiKeyInspector
{
    public bool IsConfigured(string provider) => !string.IsNullOrWhiteSpace(GetRawKey(provider));

    public string? GetMasked(string provider)
    {
        var key = GetRawKey(provider);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var prefixLength = Math.Min(3, key.Length);
        var suffixLength = Math.Min(4, key.Length);
        return $"{key[..prefixLength]}••••{key[^suffixLength..]}";
    }

    private string? GetRawKey(string provider) =>
        string.Equals(provider, AssistantRuntimeConfig.LmStudioProviderName, StringComparison.OrdinalIgnoreCase)
            ? null
            : AssistantRuntimeConfig.ResolveOpenAiApiKey(configuration);
}
