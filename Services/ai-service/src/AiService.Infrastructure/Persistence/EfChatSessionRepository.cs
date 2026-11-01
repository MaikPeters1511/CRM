using AiService.Application.Abstractions;
using AiService.Domain.Chat;
using Microsoft.EntityFrameworkCore;

namespace AiService.Infrastructure.Persistence;

public sealed class EfChatSessionRepository(AiDbContext dbContext) : IChatSessionRepository
{
    public async Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await dbContext.ChatSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public async Task AddAsync(ChatSession session, CancellationToken cancellationToken) =>
        await dbContext.ChatSessions.AddAsync(session, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
