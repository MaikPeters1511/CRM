using AiService.Domain.Settings;

namespace AiService.Tests.Domain;

public class AssistantSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task SeedFromStartupConfig_CreatesEnabledSettingsWithoutPendingRestart()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now);

        await Assert.That(settings.Enabled).IsTrue();
        await Assert.That(settings.RestartRequired).IsFalse();
        await Assert.That(settings.LastIndexSyncAt).IsNull();
        await Assert.That(settings.Provider).IsEqualTo("OpenAi");
    }

    [Test]
    public async Task ApplyAdminUpdate_ChangingOnlyEnabled_DoesNotRequireRestart()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now);

        settings.ApplyAdminUpdate(false, "OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now.AddMinutes(5));

        await Assert.That(settings.Enabled).IsFalse();
        await Assert.That(settings.RestartRequired).IsFalse();
    }

    [Test]
    public async Task ApplyAdminUpdate_ChangingProvider_SetsRestartRequired()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now);

        settings.ApplyAdminUpdate(true, "LmStudio", "local-model", "http://qdrant:6333", Now.AddMinutes(5));

        await Assert.That(settings.RestartRequired).IsTrue();
        await Assert.That(settings.Provider).IsEqualTo("LmStudio");
        await Assert.That(settings.Model).IsEqualTo("local-model");
    }

    [Test]
    public async Task ReconcileWithStartupConfig_ClearsRestartRequiredAndKeepsEnabledAndLastSync()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now);
        settings.ApplyAdminUpdate(false, "LmStudio", "local-model", "http://qdrant:6333", Now.AddMinutes(5));
        settings.RecordIndexSync(Now.AddMinutes(10));

        settings.ReconcileWithStartupConfig("LmStudio", "local-model", "http://qdrant:6333", Now.AddHours(1));

        await Assert.That(settings.RestartRequired).IsFalse();
        await Assert.That(settings.Enabled).IsFalse();
        await Assert.That(settings.LastIndexSyncAt).IsEqualTo(Now.AddMinutes(10));
    }

    [Test]
    public async Task RecordIndexSync_SetsLastIndexSyncAt()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now);

        settings.RecordIndexSync(Now.AddHours(2));

        await Assert.That(settings.LastIndexSyncAt).IsEqualTo(Now.AddHours(2));
    }
}
