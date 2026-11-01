using AiService.Application.Abstractions;

namespace AiService.Infrastructure.Llm;

/// <summary>
/// Requirement "Konfigurierbare LLM-Anbindung (OpenAI/LMStudio)": LMStudio exponiert lokal ein zur
/// OpenAI Chat-Completions-API kompatibles Schema - identische Anfrage-/Antwortstruktur, daher
/// gemeinsame <see cref="OpenAiChatCompletion"/>-Hilfsklasse, nur Basis-URL/Modellname unterscheiden
/// sich ueber Konfiguration (kein Codeunterschied in der Orchestrierungslogik, HandleChatMessageHandler
/// kennt nur <see cref="ILlmClient"/>).
/// </summary>
public sealed class LmStudioLlmClient(HttpClient httpClient, string model) : ILlmClient
{
    public async Task<string> CompleteAsync(string systemPrompt, string userContext, CancellationToken cancellationToken)
    {
        try
        {
            return await OpenAiChatCompletion.CompleteAsync(httpClient, model, systemPrompt, userContext, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new LlmUnavailableException("LMStudio-Endpunkt ist aktuell nicht erreichbar.", ex);
        }
    }
}
