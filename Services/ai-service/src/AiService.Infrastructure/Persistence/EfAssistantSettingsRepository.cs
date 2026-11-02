using AiService.Application.Abstractions;
using AiService.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace AiService.Infrastructure.Persistence;

public sealed class EfAssistantSettingsRepository(AiDbContext dbContext) : IAssistantSettingsRepository
{
    public Task<AssistantSettings?> GetAsync(CancellationToken cancellationToken) =>
        dbContext.AssistantSettings.FirstOrDefaultAsync(s => s.Id == AssistantSettings.WellKnownId, cancellationToken);

    public async Task AddAsync(AssistantSettings settings, CancellationToken cancellationToken) =>
        await dbContext.AssistantSettings.AddAsync(settings, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
