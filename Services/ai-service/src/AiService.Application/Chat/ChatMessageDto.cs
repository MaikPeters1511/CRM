using AiService.Domain.Chat;

namespace AiService.Application.Chat;

public sealed record ChatMessageDto(Guid SessionId, string Role, string Content, DateTimeOffset CreatedAt)
{
    public static ChatMessageDto FromDomain(ChatMessage message) => new(
        message.SessionId,
        message.Role == ChatRole.User ? "user" : "assistant",
        message.Content,
        message.CreatedAt);
}
