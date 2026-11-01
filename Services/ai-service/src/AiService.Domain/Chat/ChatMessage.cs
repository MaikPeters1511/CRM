using AiService.Domain.Common;

namespace AiService.Domain.Chat;

/// <summary>
/// Einzelne Chat-Nachricht innerhalb einer <see cref="ChatSession"/> (ki-terminassistent.openspec.md,
/// Abschnitt 5, <c>ChatMessageDto</c>). Reine Verlaufs-/Protokolldaten - keine Terminlogik.
/// </summary>
public sealed class ChatMessage : Entity
{
    public Guid SessionId { get; private set; }
    public ChatRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private ChatMessage()
    {
    }

    internal ChatMessage(Guid id, Guid sessionId, ChatRole role, string content, DateTimeOffset createdAt) : base(id)
    {
        SessionId = sessionId;
        Role = role;
        Content = content;
        CreatedAt = createdAt;
    }
}
