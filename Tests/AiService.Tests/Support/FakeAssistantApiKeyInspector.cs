using AiService.Application.Abstractions;

namespace AiService.Tests.Support;

public sealed class FakeAssistantApiKeyInspector : IAssistantApiKeyInspector
{
    public bool Configured { get; set; }

    public string? Masked { get; set; }

    public bool IsConfigured(string provider) => Configured;

    public string? GetMasked(string provider) => Masked;
}
