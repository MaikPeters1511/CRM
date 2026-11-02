using AiService.Domain.Settings;

namespace AiService.Application.Abstractions;

public interface IAssistantSettingsRepository
{
    Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken);

    Task AddAsync(AssistantSettings settings, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
