using AiService.Application.Abstractions;

namespace AiService.Infrastructure.Llm;

/// <summary>
/// Requirement "Konfigurierbare LLM-Anbindung (OpenAI/LMStudio)": Aufruf der OpenAI Chat-Completions-API.
/// Polly-Resilienz (Timeout/Retry/Circuit-Breaker) ist am <see cref="HttpClient"/> konfiguriert
/// (AiService.Infrastructure.DependencyInjection) - diese Klasse uebersetzt eine nach Ausschoepfung
/// der Retries weiterhin fehlschlagende Anfrage in <see cref="LlmUnavailableException"/>
/// (Requirement "Resilienz bei Nichtverfügbarkeit der KI-API").
/// </summary>
public sealed class OpenAiLlmClient(HttpClient httpClient, string model) : ILlmClient
{
    public async Task<string> CompleteAsync(string systemPrompt, string userContext, CancellationToken cancellationToken)
    {
        try
        {
            return await OpenAiChatCompletion.CompleteAsync(httpClient, model, systemPrompt, userContext, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new LlmUnavailableException("OpenAI API ist aktuell nicht erreichbar.", ex);
        }
    }
}
