using AiService.Application.Abstractions;

namespace AiService.Application.Chat.GetHistory;

public static class GetChatHistoryHandler
{
    public static async Task<GetChatHistoryResult> Handle(
        GetChatHistoryQuery query, IChatSessionRepository repository, CancellationToken cancellationToken)
    {
        var session = await repository.GetByIdAsync(query.SessionId, cancellationToken);
        if (session is null || !session.IsOwnedBy(query.CallerUserId))
        {
            return GetChatHistoryResult.NotFound();
        }

        return GetChatHistoryResult.Success(session.Messages.Select(ChatMessageDto.FromDomain).ToList());
    }
}
