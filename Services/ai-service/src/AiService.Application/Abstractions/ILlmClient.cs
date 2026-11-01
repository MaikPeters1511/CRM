namespace AiService.Application.Abstractions;

/// <summary>
/// Provider-Abstraktion fuer den formulierenden LLM-Call (ki-terminassistent.openspec.md,
/// Requirement "Konfigurierbare LLM-Anbindung (OpenAI/LMStudio)") - Auswahl der Implementierung
/// (<c>OpenAiLlmClient</c>/<c>LmStudioLlmClient</c>) erfolgt ueber Konfiguration/DI
/// (AiService.Infrastructure.DependencyInjection), ohne Codeaenderung in der Orchestrierungslogik.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Formuliert die Chat-Antwort aus dem bereits minimierten Kontext (Requirement
    /// "Datenminimierung bei externen LLM-Aufrufen" - <paramref name="systemPrompt"/>/
    /// <paramref name="userContext"/> duerfen KEINE vollstaendige Kundenhistorie enthalten).
    /// </summary>
    /// <exception cref="LlmUnavailableException">
    /// Nach Ausschoepfung der Polly-Retry-Policy (Requirement "Resilienz bei Nichtverfuegbarkeit
    /// der KI-API") - der Aufrufer SHALL dies in eine klare, verstaendliche Fehlermeldung statt
    /// eines Absturzes uebersetzen.
    /// </exception>
    Task<string> CompleteAsync(string systemPrompt, string userContext, CancellationToken cancellationToken);
}

public sealed class LlmUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
