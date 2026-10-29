using System.Security.Claims;
using CatalogService.Application.Services.Create;
using CatalogService.Application.Services.Deactivate;
using CatalogService.Application.Services.GetById;
using CatalogService.Application.Services.Search;
using CatalogService.Application.Services.Update;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Wolverine;

namespace CatalogService.Web.Controllers;

/// <summary>
/// Leistungskatalog (UserStories/leistungskatalog.openspec.md). Alle Endpoints erfordern ein
/// gueltiges JWT; Lesen ist fuer <c>Employee</c> und <c>Admin</c> erlaubt, Anlegen/Bearbeiten/
/// Deaktivieren ist auf Rolle <c>Admin</c> beschraenkt (Abschnitt 5).
/// </summary>
[Authorize]
[ApiController]
[Route("api/services")]
public sealed class ServicesController(IMessageBus messageBus) : ControllerBase
{
    /// <summary>Paginierte, nach Name durchsuchbare und nach Status (aktiv/inaktiv) filterbare Leistungsliste.</summary>
    [HttpGet]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<ActionResult<SearchServicesResult>> Search(
        [FromQuery] string? search,
        [FromQuery] bool? status,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await messageBus.InvokeAsync<SearchServicesResult>(
            new SearchServicesQuery(search, status, page), cancellationToken);

        return Ok(result);
    }

    /// <summary>Leistungsdetails. Liefert <c>404</c>, wenn die Leistung nicht existiert.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await messageBus.InvokeAsync<GetServiceByIdResult>(
            new GetServiceByIdQuery(id), cancellationToken);

        return result.Found ? Ok(result.Service) : NotFound();
    }

    /// <summary>Legt eine neue Leistung an (Pflichtfeld: Name; Beschreibung/Standarddauer optional).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateServiceCommand(
            request.Name,
            request.Description,
            request.DefaultDurationMinutes,
            GetActorId());

        var result = await messageBus.InvokeAsync<CreateServiceResult>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return ValidationProblem(BuildValidationProblem(result.Errors));
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Service!.Id }, result.Service);
    }

    /// <summary>Aktualisiert eine bestehende Leistung (setzt <c>ModifiedAt</c>/<c>ModifiedBy</c>).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateServiceCommand(
            id,
            request.Name,
            request.Description,
            request.DefaultDurationMinutes,
            GetActorId());

        var result = await messageBus.InvokeAsync<UpdateServiceResult>(command, cancellationToken);

        return result.Status switch
        {
            UpdateServiceStatus.Success => Ok(result.Service),
            UpdateServiceStatus.NotFound => NotFound(),
            _ => ValidationProblem(BuildValidationProblem(result.Errors)),
        };
    }

    /// <summary>
    /// Deaktiviert eine Leistung (Soft-Delete, nur Rolle <c>Admin</c>). Bestehende
    /// Mitarbeiter-Zuordnungen (employee-service, Snapshot-Werte) bleiben unveraendert.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeactivateServiceCommand(id, GetActorId());

        var result = await messageBus.InvokeAsync<DeactivateServiceResult>(command, cancellationToken);

        return result.Status switch
        {
            DeactivateServiceStatus.Success => NoContent(),
            _ => NotFound(),
        };
    }

    private string GetActorId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? "unknown";

    private static ModelStateDictionary BuildValidationProblem(IReadOnlyList<string> errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in errors)
        {
            modelState.AddModelError(string.Empty, error);
        }

        return modelState;
    }
}

public sealed record CreateServiceRequest(string Name, string? Description, int? DefaultDurationMinutes);

public sealed record UpdateServiceRequest(string Name, string? Description, int? DefaultDurationMinutes);
