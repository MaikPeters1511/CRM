using AiService.Application.Settings.TriggerIndexSync;
using AiService.Domain.Settings;
using AiService.Tests.Support;

namespace AiService.Tests.Application;

public class TriggerIndexSyncHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_ReturnsPreviousAndNewSyncTimestamps()
    {
        var previousSyncAt = Now.AddHours(-20);
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now.AddDays(-2));
        settings.RecordIndexSync(previousSyncAt);
        var settingsRepository = new FakeAssistantSettingsRepository(settings);

        var result = await TriggerIndexSyncHandler.Handle(
            new TriggerIndexSyncCommand(),
            settingsRepository,
            new FakeCatalogQueryClient(),
            new FakeEmployeeQueryClient(),
            new FakeAvailabilityIndexClient(),
            new FakeTimeProvider(Now),
            CancellationToken.None);

        await Assert.That(result.PreviousSyncAt).IsEqualTo(previousSyncAt);
        await Assert.That(result.SyncedAt).IsEqualTo(Now);
        await Assert.That(settings.LastIndexSyncAt).IsEqualTo(Now);
    }

    [Test]
    public async Task Handle_WhenNeverSyncedBefore_ReturnsNullPreviousSyncAt()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now.AddDays(-2));
        var settingsRepository = new FakeAssistantSettingsRepository(settings);

        var result = await TriggerIndexSyncHandler.Handle(
            new TriggerIndexSyncCommand(),
            settingsRepository,
            new FakeCatalogQueryClient(),
            new FakeEmployeeQueryClient(),
            new FakeAvailabilityIndexClient(),
            new FakeTimeProvider(Now),
            CancellationToken.None);

        await Assert.That(result.PreviousSyncAt).IsNull();
    }
}
