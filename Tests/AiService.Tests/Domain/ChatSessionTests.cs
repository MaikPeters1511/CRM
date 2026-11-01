using AiService.Domain.Chat;

namespace AiService.Tests.Domain;

public class ChatSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Start_WithOwnerUserId_ReturnsSessionWithUniqueIdAndNoMessages()
    {
        var session = ChatSession.Start("user-1", Now);

        await Assert.That(session.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(session.OwnerUserId).IsEqualTo("user-1");
        await Assert.That(session.Messages).IsEmpty();
    }

    [Test]
    public async Task AddMessage_AppendsMessageWithSessionId()
    {
        var session = ChatSession.Start("user-1", Now);

        var message = session.AddMessage(ChatRole.User, "Freier Termin für Farbe?", Now.AddMinutes(1));

        await Assert.That(session.Messages).Count().IsEqualTo(1);
        await Assert.That(message.SessionId).IsEqualTo(session.Id);
        await Assert.That(message.Role).IsEqualTo(ChatRole.User);
        await Assert.That(message.Content).IsEqualTo("Freier Termin für Farbe?");
    }

    [Test]
    public async Task IsOwnedBy_WithDifferentUserId_ReturnsFalse()
    {
        var session = ChatSession.Start("user-1", Now);

        await Assert.That(session.IsOwnedBy("user-1")).IsTrue();
        await Assert.That(session.IsOwnedBy("user-2")).IsFalse();
    }
}
