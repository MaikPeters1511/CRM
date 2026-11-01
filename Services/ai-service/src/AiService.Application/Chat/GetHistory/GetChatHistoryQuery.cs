namespace AiService.Application.Chat.GetHistory;

/// <summary>
/// <c>GET /api/assistant/sessions/{sessionId}/history</c> (ki-terminassistent.openspec.md, Abschnitt 5):
/// liefert den Chatverlauf nur fuer den eigenen Nutzer - Chatverlaeufe sind nicht
/// mitarbeiteruebergreifend einsehbar, auch nicht fuer Admins.
/// </summary>
public sealed record GetChatHistoryQuery(Guid SessionId, string CallerUserId);

public enum GetChatHistoryStatus
{
    Success,
    NotFound,
}

public sealed record GetChatHistoryResult(GetChatHistoryStatus Status, IReadOnlyList<ChatMessageDto> Messages)
{
    public static GetChatHistoryResult Success(IReadOnlyList<ChatMessageDto> messages) =>
        new(GetChatHistoryStatus.Success, messages);

    public static GetChatHistoryResult NotFound() => new(GetChatHistoryStatus.NotFound, []);
}
