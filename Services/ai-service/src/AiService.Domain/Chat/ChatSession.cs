using AiService.Domain.Common;

namespace AiService.Domain.Chat;

/// <summary>
/// Aggregate Root fuer den Chatverlauf des KI-Terminassistenten (ki-terminassistent.openspec.md,
/// Abschnitt 5/7). Haelt bewusst KEINE Terminlogik - die eigentliche Verfuegbarkeitsberechnung
/// bleibt alleinig in appointment-service/employee-service (Abschnitt 7, Design). Eine Session
/// gehoert genau einem Nutzer (<see cref="OwnerUserId"/>, JWT-Subject) - Chatverlaeufe sind nicht
/// mitarbeiteruebergreifend einsehbar (Requirement "GET /api/assistant/sessions/{sessionId}/history").
/// </summary>
public sealed class ChatSession : AggregateRoot
{
    private readonly List<ChatMessage> _messages = [];

    public string OwnerUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<ChatMessage> Messages => _messages.AsReadOnly();

    private ChatSession()
    {
    }

    private ChatSession(Guid id, string ownerUserId, DateTimeOffset createdAt) : base(id)
    {
        OwnerUserId = ownerUserId;
        CreatedAt = createdAt;
    }

    public static ChatSession Start(string ownerUserId, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), ownerUserId, createdAt);

    public ChatMessage AddMessage(ChatRole role, string content, DateTimeOffset createdAt)
    {
        var message = new ChatMessage(Guid.NewGuid(), Id, role, content, createdAt);
        _messages.Add(message);
        return message;
    }

    public bool IsOwnedBy(string userId) => OwnerUserId == userId;
}
