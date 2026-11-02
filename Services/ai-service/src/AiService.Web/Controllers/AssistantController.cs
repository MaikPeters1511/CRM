using System.Security.Claims;
using AiService.Application.Abstractions;
using AiService.Application.Chat;
using AiService.Application.Chat.GetHistory;
using AiService.Application.Chat.HandleChatMessage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace AiService.Web.Controllers;

/// <summary>
/// KI-Terminassistent (ki-terminassistent.openspec.md, Abschnitt 5). Rollen <c>Employee</c> und
/// <c>Admin</c> duerfen den Chat nutzen; die RBAC-Einschraenkung der Vorschlagsergebnisse erfolgt
/// serverseitig anhand der <c>EmployeeId</c>-Claim des JWT (Requirement "RBAC-konforme Sichtbarkeit
/// von Terminvorschlägen"), niemals im Frontend.
/// </summary>
[Authorize]
[ApiController]
[Route("api/assistant")]
public sealed class AssistantController(IMessageBus messageBus, IAssistantSettingsRepository settingsRepository) : ControllerBase
{
    private const string AssistantDisabledMessage =
        "Der Assistent wurde von einem Administrator deaktiviert. Bitte nutzen Sie die Kalenderansicht.";

    /// <summary>
    /// Nimmt <c>{ sessionId, message }</c> entgegen, liefert <c>{ reply, suggestions }</c>. Der
    /// Assistent legt, aendert oder storniert selbst niemals einen Termin - jeder Vorschlag ist
    /// unverbindlich (Requirement "Vorschlag ist unverbindlich und erfordert bestätigte Terminanlage").
    /// Respektiert das Enabled-Flag aus ki-einstellungen-backend.openspec.md, live aus der Datenbank
    /// gelesen - ein Admin kann den Assistenten ohne Neustart deaktivieren (docs/adr/0004).
    /// </summary>
    [HttpPost("chat")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken);
        if (settings is { Enabled: false })
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ChatResponse(AssistantDisabledMessage, []));
        }

        var command = new HandleChatMessageCommand(
            request.SessionId, request.Message, GetActorId(), IsAdmin(), GetCurrentEmployeeId());

        var result = await messageBus.InvokeAsync<HandleChatMessageResult>(command, cancellationToken);

        return Ok(new ChatResponse(result.Reply, result.Suggestions));
    }

    /// <summary>
    /// Liefert den Chatverlauf einer Session - nur fuer den eigenen Nutzer, nicht
    /// mitarbeiteruebergreifend einsehbar (auch nicht fuer Admins).
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/history")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> GetHistory(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await messageBus.InvokeAsync<GetChatHistoryResult>(
            new GetChatHistoryQuery(sessionId, GetActorId()), cancellationToken);

        return result.Status == GetChatHistoryStatus.Success ? Ok(result.Messages) : NotFound();
    }

    private bool IsAdmin() => User.IsInRole("Admin");

    private Guid? GetCurrentEmployeeId() =>
        !IsAdmin() && Guid.TryParse(GetActorId(), out var employeeId) ? employeeId : null;

    private string GetActorId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? "unknown";
}

public sealed record ChatRequest(Guid? SessionId, string Message);

public sealed record ChatResponse(string Reply, IReadOnlyList<AppointmentSuggestionDto> Suggestions);
