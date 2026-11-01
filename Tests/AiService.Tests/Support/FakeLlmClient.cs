using AiService.Application.Abstractions;

namespace AiService.Tests.Support;

public sealed class FakeLlmClient : ILlmClient
{
    public bool Unavailable { get; set; }
    public string ReplyToReturn { get; set; } = "Hier sind Ihre Terminvorschläge.";
    public string? LastSystemPrompt { get; private set; }
    public string? LastUserContext { get; private set; }

    public Task<string> CompleteAsync(string systemPrompt, string userContext, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            throw new LlmUnavailableException("LLM nicht erreichbar (Test).");
        }

        LastSystemPrompt = systemPrompt;
        LastUserContext = userContext;
        return Task.FromResult(ReplyToReturn);
    }
}
