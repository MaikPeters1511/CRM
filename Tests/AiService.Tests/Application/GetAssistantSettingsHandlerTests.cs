using AiService.Application.Settings.GetAssistantSettings;
using AiService.Domain.Settings;
using AiService.Tests.Support;

namespace AiService.Tests.Application;

public class GetAssistantSettingsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_WithApiKeyConfigured_ReturnsMaskedKeyAndNeverRawKey()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now);
        var repository = new FakeAssistantSettingsRepository(settings);
        var apiKeyInspector = new FakeAssistantApiKeyInspector { Configured = true, Masked = "sk-••••1234" };

        var result = await GetAssistantSettingsHandler.Handle(new GetAssistantSettingsQuery(), repository, apiKeyInspector, CancellationToken.None);

        await Assert.That(result.ApiKeyConfigured).IsTrue();
        await Assert.That(result.MaskedApiKey).IsEqualTo("sk-••••1234");
        await Assert.That(result.Provider).IsEqualTo("OpenAi");
        await Assert.That(result.LastIndexSyncAt).IsNull();
    }

    [Test]
    public async Task Handle_WithoutApiKeyConfigured_ReturnsNotConfiguredWithoutMaskedValue()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("LmStudio", "local-model", "http://qdrant:6333", Now);
        var repository = new FakeAssistantSettingsRepository(settings);
        var apiKeyInspector = new FakeAssistantApiKeyInspector { Configured = false, Masked = null };

        var result = await GetAssistantSettingsHandler.Handle(new GetAssistantSettingsQuery(), repository, apiKeyInspector, CancellationToken.None);

        await Assert.That(result.ApiKeyConfigured).IsFalse();
        await Assert.That(result.MaskedApiKey).IsNull();
    }

    [Test]
    public async Task Handle_ReturnsLastIndexSyncAtFromSettings()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now);
        settings.RecordIndexSync(Now.AddHours(3));
        var repository = new FakeAssistantSettingsRepository(settings);

        var result = await GetAssistantSettingsHandler.Handle(
            new GetAssistantSettingsQuery(), repository, new FakeAssistantApiKeyInspector(), CancellationToken.None);

        await Assert.That(result.LastIndexSyncAt).IsEqualTo(Now.AddHours(3));
    }
}
