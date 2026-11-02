using IdentityService.Application.Audit;
using IdentityService.Application.Auth;
using IdentityService.Domain.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Web.Controllers;

/// <summary>Admin-Einsicht in das sicherheitsrelevante Audit-Log sowie Erfassung fachlicher Aenderungen (identity-audit-log.openspec.md).</summary>
[ApiController]
[Route("api/audit-log")]
[Authorize]
public sealed class AuditLogController(
    GetAuditLogHandler getHandler,
    ExportAuditLogCsvHandler exportHandler,
    IUserRepository userRepository,
    IAuditLogger auditLogger) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Get(
        [FromQuery] AuditEventType? eventType,
        [FromQuery] Guid? userId,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var filter = new AuditLogFilter(eventType, userId, fromUtc, toUtc);
        var result = await getHandler.HandleAsync(new GetAuditLogQuery(filter, page, pageSize), cancellationToken);

        var actorLabels = new Dictionary<Guid, string>();
        foreach (var actorId in result.Items.Select(i => i.ActorUserId).Distinct())
        {
            if (actorId == Guid.Empty)
            {
                actorLabels[actorId] = "System / Unbekannt";
                continue;
            }

            var user = await userRepository.FindByIdAsync(actorId, cancellationToken);
            actorLabels[actorId] = user is not null
                ? $"{user.DisplayName} ({user.Email})"
                : actorId.ToString();
        }

        var items = result.Items
            .Select(entry => ToResponse(entry, actorLabels.GetValueOrDefault(entry.ActorUserId, entry.ActorUserId.ToString())))
            .ToList();

        return Ok(new AuditLogPageResponse(items, result.TotalCount, page, pageSize));
    }

    [HttpPost]
    public async Task<IActionResult> Record(
        [FromBody] CreateAuditLogEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = User.RequireUserId();
        var eventType = Enum.TryParse<AuditEventType>(request.Action, ignoreCase: true, out var parsed)
            ? parsed
            : AuditEventType.DataChanged;

        await auditLogger.LogAsync(
            eventType,
            actorUserId,
            request.TargetUserId,
            request.Details ?? string.Empty,
            HttpContext.ClientIp(),
            cancellationToken);

        return NoContent();
    }

    [HttpGet("export")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Export(
        [FromQuery] AuditEventType? eventType,
        [FromQuery] Guid? userId,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default)
    {
        var filter = new AuditLogFilter(eventType, userId, fromUtc, toUtc);
        var csv = await exportHandler.HandleAsync(new ExportAuditLogCsvQuery(filter), cancellationToken);

        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "audit-log.csv");
    }

    private static AuditLogEntryResponse ToResponse(AuditLogEntry entry, string actorLabel) => new(
        entry.Id,
        entry.OccurredAtUtc,
        entry.EventType,
        entry.EventType.ToString(),
        entry.ActorUserId,
        actorLabel,
        entry.TargetUserId,
        entry.Details,
        entry.IpAddress);
}

public sealed record CreateAuditLogEntryRequest(
    string Action,
    string? Details,
    Guid? TargetUserId = null);

public sealed record AuditLogEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    AuditEventType EventType,
    string Action,
    Guid ActorUserId,
    string Actor,
    Guid? TargetUserId,
    string Details,
    string? IpAddress);

public sealed record AuditLogPageResponse(IReadOnlyList<AuditLogEntryResponse> Items, int TotalCount, int Page, int PageSize);
