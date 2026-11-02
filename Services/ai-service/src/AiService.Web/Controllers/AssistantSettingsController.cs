using AiService.Application.Settings;
using AiService.Application.Settings.GetAssistantSettings;
using AiService.Application.Settings.TriggerIndexSync;
using AiService.Application.Settings.UpdateAssistantSettings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace AiService.Web.Controllers;

/// <summary>
/// Admin-only Konfigurations-API des KI-Assistenten (ki-einstellungen-backend.openspec.md). Niemals
/// den API-Key im Klartext zurueckgeben oder entgegennehmen - siehe
/// <see cref="GetAssistantSettingsHandler"/>/docs/adr/0004-ki-assistent-einstellungen-laufzeit.md.
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/assistant/settings")]
public sealed class AssistantSettingsController(IMessageBus messageBus) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AssistantSettingsDto>> GetSettings(CancellationToken cancellationToken)
    {
        var result = await messageBus.InvokeAsync<AssistantSettingsDto>(new GetAssistantSettingsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<AssistantSettingsDto>> UpdateSettings(
        [FromBody] UpdateAssistantSettingsRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateAssistantSettingsCommand(request.Enabled, request.Provider, request.Model, request.QdrantBaseUrl);
        var result = await messageBus.InvokeAsync<AssistantSettingsDto>(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sync-now")]
    public async Task<ActionResult<SyncNowResponse>> SyncNow(CancellationToken cancellationToken)
    {
        var result = await messageBus.InvokeAsync<TriggerIndexSyncResult>(new TriggerIndexSyncCommand(), cancellationToken);
        return Ok(new SyncNowResponse(result.PreviousSyncAt, result.SyncedAt));
    }
}

/// <summary>
/// Request-DTO fuer <c>PUT /api/assistant/settings</c>. Enthaelt bewusst KEIN API-Key-Feld - ein
/// etwaiges zusaetzliches Feld im JSON-Body wird von System.Text.Json ignoriert (Requirement
/// "PUT-Body enthält einen API-Key").
/// </summary>
public sealed record UpdateAssistantSettingsRequest(bool Enabled, string Provider, string Model, string QdrantBaseUrl);

public sealed record SyncNowResponse(DateTimeOffset? PreviousSyncAt, DateTimeOffset SyncedAt);
