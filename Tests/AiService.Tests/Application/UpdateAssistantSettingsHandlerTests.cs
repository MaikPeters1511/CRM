using AiService.Application.Settings.UpdateAssistantSettings;
using AiService.Domain.Settings;
using AiService.Tests.Support;

namespace AiService.Tests.Application;

public class UpdateAssistantSettingsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_DisablingAssistant_TakesEffectImmediatelyWithoutRestartRequired()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now);
        var repository = new FakeAssistantSettingsRepository(settings);
        var command = new UpdateAssistantSettingsCommand(false, "OpenAi", "gpt-4o-mini", "http://qdrant:6333");

        var result = await UpdateAssistantSettingsHandler.Handle(
            command, repository, new FakeAssistantApiKeyInspector(), new FakeTimeProvider(Now.AddMinutes(5)), CancellationToken.None);

        await Assert.That(result.Enabled).IsFalse();
        await Assert.That(result.RestartRequired).IsFalse();
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_ChangingProvider_PersistsNewValueAndMarksRestartRequired()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now);
        var repository = new FakeAssistantSettingsRepository(settings);
        var command = new UpdateAssistantSettingsCommand(true, "LmStudio", "local-model", "http://qdrant:6333");

        var result = await UpdateAssistantSettingsHandler.Handle(
            command, repository, new FakeAssistantApiKeyInspector(), new FakeTimeProvider(Now.AddMinutes(5)), CancellationToken.None);

        await Assert.That(result.Provider).IsEqualTo("LmStudio");
        await Assert.That(result.Model).IsEqualTo("local-model");
        await Assert.That(result.RestartRequired).IsTrue();
    }

    [Test]
    public async Task Handle_AlwaysReturnsApiKeyManagementNote()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now);
        var repository = new FakeAssistantSettingsRepository(settings);
        var command = new UpdateAssistantSettingsCommand(true, "OpenAi", "gpt-4o-mini", "http://qdrant:6333");

        var result = await UpdateAssistantSettingsHandler.Handle(
            command, repository, new FakeAssistantApiKeyInspector(), new FakeTimeProvider(Now), CancellationToken.None);

        await Assert.That(result.ApiKeyManagementNote).IsNotEmpty();
    }
}
