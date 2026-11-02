using AiService.Application.IndexSync;
using AiService.Domain.Settings;
using AiService.Tests.Support;

namespace AiService.Tests.Application;

public class IndexConsistencySyncHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    // docs/adr/0003 Abschnitt 3 / ki-einstellungen-backend.openspec.md: "zuletzt synchronisiert" muss
    // nach jedem Lauf (taeglich wie on-demand) aktuell sein, damit GET /api/assistant/settings korrekt ist.
    [Test]
    public async Task Handle_RecordsLastIndexSyncAtOnSettings()
    {
        var settings = AssistantSettings.SeedFromStartupConfig("OpenAi", "gpt-4o-mini", "http://qdrant:6333", Now.AddDays(-1));
        var settingsRepository = new FakeAssistantSettingsRepository(settings);

        await IndexConsistencySyncHandler.Handle(
            new RunIndexConsistencySyncCommand(),
            new FakeCatalogQueryClient(),
            new FakeEmployeeQueryClient(),
            new FakeAvailabilityIndexClient(),
            settingsRepository,
            new FakeTimeProvider(Now),
            CancellationToken.None);

        await Assert.That(settings.LastIndexSyncAt).IsEqualTo(Now);
    }

    [Test]
    public async Task Handle_WithoutInitializedSettings_DoesNotThrow()
    {
        var settingsRepository = new FakeAssistantSettingsRepository();

        await IndexConsistencySyncHandler.Handle(
            new RunIndexConsistencySyncCommand(),
            new FakeCatalogQueryClient(),
            new FakeEmployeeQueryClient(),
            new FakeAvailabilityIndexClient(),
            settingsRepository,
            new FakeTimeProvider(Now),
            CancellationToken.None);

        await Assert.That(settingsRepository.SaveChangesCallCount).IsEqualTo(0);
    }
}
