using IdentityService.Application.Audit;
using IdentityService.Domain.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Web.Controllers;

/// <summary>Admin-Einsicht in das sicherheitsrelevante Audit-Log (identity-audit-log.openspec.md).</summary>
[ApiController]
[Route("api/audit-log")]
[Authorize(Roles = "Admin")]
public sealed class AuditLogController(
    GetAuditLogHandler getHandler,
    ExportAuditLogCsvHandler exportHandler) : ControllerBase
{
    [HttpGet]
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

        return Ok(new AuditLogPageResponse(
            result.Items.Select(ToResponse).ToList(), result.TotalCount, page, pageSize));
    }

    [HttpGet("export")]
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

    private static AuditLogEntryResponse ToResponse(AuditLogEntry entry) => new(
        entry.Id, entry.OccurredAtUtc, entry.EventType, entry.ActorUserId, entry.TargetUserId, entry.Details, entry.IpAddress);
}

public sealed record AuditLogEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    AuditEventType EventType,
    Guid ActorUserId,
    Guid? TargetUserId,
    string Details,
    string? IpAddress);

public sealed record AuditLogPageResponse(IReadOnlyList<AuditLogEntryResponse> Items, int TotalCount, int Page, int PageSize);
