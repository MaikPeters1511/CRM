using System.Security.Claims;
using EmployeeService.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace EmployeeService.Web.Controllers;

/// <summary>
/// Benachrichtigungen (abwesenheit-workflow.openspec.md, Abschnitt 5). Jeder authentifizierte
/// Mitarbeiter sieht ausschliesslich seine eigenen Benachrichtigungen (Ownership-Check analog
/// Abwesenheiten); Admins duerfen zusaetzlich fremde Benachrichtigungen als gelesen markieren.
/// </summary>
[Authorize]
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(IMessageBus messageBus) : ControllerBase
{
    /// <summary>Eigene Benachrichtigungen, neueste zuerst.</summary>
    [HttpGet]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(GetActorId(), out var actorId))
        {
            // Kein eindeutiger NameIdentifier-Claim -> fail-closed, keine Benachrichtigungen zurueckgeben.
            return Ok(Array.Empty<NotificationDto>());
        }

        var result = await messageBus.InvokeAsync<GetNotificationsResult>(
            new GetNotificationsQuery(actorId), cancellationToken);

        return Ok(result.Notifications);
    }

    /// <summary>Markiert eine Benachrichtigung als gelesen. <c>403</c> bei Fremdzugriff ohne Admin-Rolle.</summary>
    [HttpPost("{id:guid}/read")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(GetActorId(), out var actorId) && !IsAdmin())
        {
            return Forbid();
        }

        var command = new MarkNotificationAsReadCommand(id, actorId, IsAdmin());

        var result = await messageBus.InvokeAsync<MarkNotificationAsReadResult>(command, cancellationToken);

        return result.Status switch
        {
            MarkNotificationAsReadStatus.Success => NoContent(),
            MarkNotificationAsReadStatus.NotFound => NotFound(),
            _ => Forbid(),
        };
    }

    private bool IsAdmin() => User.IsInRole("Admin");

    private string GetActorId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? "unknown";
}
