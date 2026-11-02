using AiService.Application.Abstractions;
using AiService.Domain.Settings;

namespace AiService.Tests.Support;

public sealed class FakeAssistantSettingsRepository : IAssistantSettingsRepository
{
    private AssistantSettings? _settings;

    public int SaveChangesCallCount { get; private set; }

    public FakeAssistantSettingsRepository()
    {
    }

    public FakeAssistantSettingsRepository(AssistantSettings seed)
    {
        _settings = seed;
    }

    public Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(_settings);

    public Task AddAsync(AssistantSettings settings, CancellationToken cancellationToken)
    {
        _settings = settings;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
