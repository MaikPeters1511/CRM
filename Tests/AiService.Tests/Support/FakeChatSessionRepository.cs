using AiService.Application.Abstractions;
using AiService.Domain.Chat;

namespace AiService.Tests.Support;

public sealed class FakeChatSessionRepository : IChatSessionRepository
{
    public List<ChatSession> Sessions { get; } = [];
    public int SaveChangesCallCount { get; private set; }

    public Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));

    public Task AddAsync(ChatSession session, CancellationToken cancellationToken)
    {
        Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
