using System.Security.Claims;
using CustomerService.Application.Customers.Create;
using CustomerService.Application.Customers.Deactivate;
using CustomerService.Application.Customers.GetById;
using CustomerService.Application.Customers.Search;
using CustomerService.Application.Customers.Update;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Wolverine;

namespace CustomerService.Web.Controllers;

/// <summary>
/// Kundenverwaltung (kundenverwaltung.openspec.md). Alle Endpoints erfordern ein gueltiges JWT
/// (Rolle <c>Employee</c> oder <c>Admin</c>); Deaktivieren ist auf <c>Admin</c> beschraenkt.
/// </summary>
[Authorize]
[ApiController]
[Route("api/customers")]
public sealed class CustomersController(IMessageBus messageBus) : ControllerBase
{
    /// <summary>Paginierte, nach Name/E-Mail/Telefonnummer durchsuchbare und nach Status filterbare Kundenliste.</summary>
    [HttpGet]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<ActionResult<SearchCustomersResult>> Search(
        [FromQuery] string? search,
        [FromQuery] bool? status,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await messageBus.InvokeAsync<SearchCustomersResult>(
            new SearchCustomersQuery(search, status, page), cancellationToken);

        return Ok(result);
    }

    /// <summary>Kundendetails inklusive Adresse. Liefert <c>404</c>, wenn der Kunde nicht existiert.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await messageBus.InvokeAsync<GetCustomerByIdResult>(
            new GetCustomerByIdQuery(id), cancellationToken);

        return result.Found ? Ok(result.Customer) : NotFound();
    }

    /// <summary>Legt einen neuen Kunden an (Pflichtfelder: Vor-/Nachname, E-Mail oder Telefonnummer).</summary>
    [HttpPost]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCustomerCommand(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.Address,
            GetActorId());

        var result = await messageBus.InvokeAsync<CreateCustomerResult>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return ValidationProblem(BuildValidationProblem(result.Errors));
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Customer!.Id }, result.Customer);
    }

    /// <summary>Aktualisiert Stammdaten eines bestehenden Kunden (setzt <c>ModifiedAt</c>/<c>ModifiedBy</c>).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCustomerCommand(
            id,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.Address,
            GetActorId());

        var result = await messageBus.InvokeAsync<UpdateCustomerResult>(command, cancellationToken);

        return result.Status switch
        {
            UpdateCustomerStatus.Success => Ok(result.Customer),
            UpdateCustomerStatus.NotFound => NotFound(),
            _ => ValidationProblem(BuildValidationProblem(result.Errors)),
        };
    }

    /// <summary>
    /// Deaktiviert einen Kunden (Soft-Delete, nur Rolle <c>Admin</c>) und publiziert das Integration
    /// Event <c>CustomerDeactivated</c> fuer konsumierende Services (siehe ADR 0001, Abschnitt 2/3).
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeactivateCustomerCommand(id, GetActorId());

        var result = await messageBus.InvokeAsync<DeactivateCustomerResult>(command, cancellationToken);

        return result.Status switch
        {
            DeactivateCustomerStatus.Success => NoContent(),
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

public sealed record CreateCustomerRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    Application.Customers.AddressDto? Address);

public sealed record UpdateCustomerRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    Application.Customers.AddressDto? Address);
