namespace AiService.Application.Chat.HandleChatMessage;

/// <summary>
/// <c>POST /api/assistant/chat</c> (ki-terminassistent.openspec.md, Abschnitt 5). <paramref name="CallerEmployeeId"/>
/// und <paramref name="IsAdmin"/> werden serverseitig aus den JWT-Claims des aufrufenden Nutzers
/// gelesen (Requirement "RBAC-konforme Sichtbarkeit von Terminvorschlägen") - NIEMALS aus dem
/// Chat-Text, damit RBAC nicht ueber Chat-Eingaben umgangen werden kann.
/// </summary>
public sealed record HandleChatMessageCommand(
    Guid? SessionId,
    string Message,
    string CallerUserId,
    bool IsAdmin,
    Guid? CallerEmployeeId);

public sealed record HandleChatMessageResult(
    Guid SessionId,
    string Reply,
    IReadOnlyList<AppointmentSuggestionDto> Suggestions,
    bool Success,
    string? ErrorMessage)
{
    public static HandleChatMessageResult Failure(Guid sessionId, string errorMessage) =>
        new(sessionId, errorMessage, [], false, errorMessage);

    public static HandleChatMessageResult Forbidden() =>
        new(Guid.Empty, string.Empty, [], false, "Nicht autorisiert.");
}
