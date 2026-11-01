using AiService.Application.Chat.GetHistory;
using AiService.Domain.Chat;
using AiService.Tests.Support;

namespace AiService.Tests.Application;

public class GetChatHistoryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_WithOwnSession_ReturnsMessages()
    {
        var repository = new FakeChatSessionRepository();
        var session = ChatSession.Start("user-1", Now);
        session.AddMessage(ChatRole.User, "Hallo", Now);
        session.AddMessage(ChatRole.Assistant, "Wie kann ich helfen?", Now.AddSeconds(1));
        repository.Sessions.Add(session);

        var result = await GetChatHistoryHandler.Handle(
            new GetChatHistoryQuery(session.Id, "user-1"), repository, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(GetChatHistoryStatus.Success);
        await Assert.That(result.Messages).Count().IsEqualTo(2);
    }

    // Chatverlaeufe sind nicht mitarbeiteruebergreifend einsehbar (Requirement GET .../history)
    [Test]
    public async Task Handle_WithSessionOfAnotherUser_ReturnsNotFound()
    {
        var repository = new FakeChatSessionRepository();
        var session = ChatSession.Start("user-1", Now);
        repository.Sessions.Add(session);

        var result = await GetChatHistoryHandler.Handle(
            new GetChatHistoryQuery(session.Id, "user-2"), repository, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(GetChatHistoryStatus.NotFound);
    }

    [Test]
    public async Task Handle_WithUnknownSessionId_ReturnsNotFound()
    {
        var repository = new FakeChatSessionRepository();

        var result = await GetChatHistoryHandler.Handle(
            new GetChatHistoryQuery(Guid.NewGuid(), "user-1"), repository, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(GetChatHistoryStatus.NotFound);
    }
}
