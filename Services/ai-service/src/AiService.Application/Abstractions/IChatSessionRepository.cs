using AiService.Domain.Chat;

namespace AiService.Application.Abstractions;

public interface IChatSessionRepository
{
    Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken);

    Task AddAsync(ChatSession session, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
