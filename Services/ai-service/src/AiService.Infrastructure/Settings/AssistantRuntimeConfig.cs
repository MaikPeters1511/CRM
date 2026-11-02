using Microsoft.Extensions.Configuration;

namespace AiService.Infrastructure.Settings;

/// <summary>
/// Liest die tatsaechlich beim Start wirksame Provider-/Modell-/Qdrant-URL-Konfiguration -
/// dieselbe Fallback-Logik wie <c>AiService.Infrastructure.DependencyInjection.AddLlmClient</c>/
/// <c>AddQdrantClient</c>, hier extrahiert, damit <c>AssistantSettingsSeedHostedService</c> (docs/adr/0004)
/// exakt dieselben Werte lesen kann, ohne die DI-Verdrahtung dieser Clients zu veraendern.
/// </summary>
public static class AssistantRuntimeConfig
{
    public const string OpenAiProviderName = "OpenAi";
    public const string LmStudioProviderName = "LmStudio";

    public static string ResolveProvider(IConfiguration configuration) =>
        FirstNonBlank(configuration["LlmProvider"], Environment.GetEnvironmentVariable("LLM_PROVIDER")) ?? OpenAiProviderName;

    public static string ResolveModel(IConfiguration configuration, string provider) =>
        string.Equals(provider, LmStudioProviderName, StringComparison.OrdinalIgnoreCase)
            ? FirstNonBlank(configuration["LmStudio:Model"], Environment.GetEnvironmentVariable("LMSTUDIO_MODEL")) ?? "local-model"
            : FirstNonBlank(configuration["OpenAi:Model"], Environment.GetEnvironmentVariable("OPENAI_MODEL")) ?? "gpt-4o-mini";

    public static string ResolveQdrantBaseUrl(IConfiguration configuration) =>
        FirstNonBlank(configuration["Qdrant:BaseUrl"], Environment.GetEnvironmentVariable("QDRANT_BASE_URL")) ?? "http://qdrant:6333";

    /// <summary>Niemals den Klartext-Key zurueckgeben - nur zum Maskieren im Aufrufer verwenden.</summary>
    public static string? ResolveOpenAiApiKey(IConfiguration configuration) =>
        FirstNonBlank(configuration["OpenAi:ApiKey"], Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

    // appsettings.json setzt mehrere dieser Keys bewusst auf "" statt sie wegzulassen (Platzhalter
    // fuer Secrets/Umgebungsvariablen) - ein leerer String SHALL wie "nicht gesetzt" behandelt werden,
    // sonst wuerde z.B. "Qdrant:BaseUrl": "" faelschlich den Default "http://qdrant:6333" ueberschreiben.
    private static string? FirstNonBlank(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
}
