using System.Security.Claims;
using AppointmentService.Application.Appointments.Calendar;
using AppointmentService.Application.Appointments.Cancel;
using AppointmentService.Application.Appointments.Create;
using AppointmentService.Application.Appointments.GetById;
using AppointmentService.Application.Appointments.Reschedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Wolverine;

namespace AppointmentService.Web.Controllers;

/// <summary>
/// Terminverwaltung (terminverwaltung.openspec.md) inkl. RBAC: Rolle <c>Employee</c> sieht/bearbeitet
/// ausschliesslich eigene Termine (Ownership-Check gegen <c>EmployeeId</c>, <c>403</c> bei Fremdzugriff);
/// Rolle <c>Admin</c> hat uneingeschraenkten Zugriff auf alle Termine.
/// </summary>
[Authorize]
[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController(IMessageBus messageBus) : ControllerBase
{
    /// <summary>
    /// terminverwaltung.openspec.md, Abschnitt 5: <c>GET /api/appointments?employeeId=&amp;customerId=&amp;from=&amp;to=</c>.
    /// This is also the exact contract consumed synchronously by employee-service's
    /// <c>IAppointmentAvailabilityClient</c> (ADR 0001, Abschnitt 2) - do not change the response
    /// shape without updating employee-service's <c>AppointmentAvailabilityHttpClient</c> as well.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> GetCalendar(
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? customerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var result = await messageBus.InvokeAsync<GetCalendarResult>(
            new GetCalendarQuery(employeeId, customerId, from, to, IsAdmin(), GetCurrentEmployeeId()), cancellationToken);

        return Ok(result.Appointments);
    }

    /// <summary>Termindetails. Liefert nur Termine, auf die der Aufrufer laut Ownership-Regel zugreifen darf.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await messageBus.InvokeAsync<GetAppointmentByIdResult>(
            new GetAppointmentByIdQuery(id, IsAdmin(), GetCurrentEmployeeId()), cancellationToken);

        return result.Found ? Ok(result.Appointment) : NotFound();
    }

    /// <summary>
    /// Legt einen Termin an (inkl. Kollisionsprüfung, Validierung von Kunde/Mitarbeiter via
    /// Inter-Service-Calls). Liefert <c>409</c> bei Doppelbuchung, <c>404</c> bei unbekanntem
    /// Kunden/Mitarbeiter, <c>503</c> bei nicht erreichbaren Abhaengigkeiten.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateAppointmentCommand(
            request.CustomerId, request.EmployeeId, request.ServiceId, request.Start, request.End, GetActorId());

        var result = await messageBus.InvokeAsync<CreateAppointmentResult>(command, cancellationToken);

        return result.Status switch
        {
            CreateAppointmentStatus.Success =>
                CreatedAtAction(nameof(GetById), new { id = result.Appointment!.Id }, result.Appointment),
            CreateAppointmentStatus.Conflict => Conflict(BuildProblem(result.Errors)),
            CreateAppointmentStatus.CustomerNotFound => NotFound(BuildProblem(result.Errors)),
            CreateAppointmentStatus.EmployeeNotFound => NotFound(BuildProblem(result.Errors)),
            CreateAppointmentStatus.DependencyUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, BuildProblem(result.Errors)),
            _ => ValidationProblem(BuildValidationProblem(result.Errors)),
        };
    }

    /// <summary>Verschiebt einen Termin auf ein neues, kollisionsfreies Zeitfenster. <c>403</c> bei Fremdzugriff ohne Admin-Rolle.</summary>
    [HttpPut("{id:guid}/reschedule")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> Reschedule(
        Guid id, [FromBody] RescheduleAppointmentRequest request, CancellationToken cancellationToken)
    {
        var command = new RescheduleAppointmentCommand(
            id, request.NewStart, request.NewEnd, GetActorId(), IsAdmin(), GetCurrentEmployeeId());

        var result = await messageBus.InvokeAsync<RescheduleAppointmentResult>(command, cancellationToken);

        return result.Status switch
        {
            RescheduleAppointmentStatus.Success => Ok(result.Appointment),
            RescheduleAppointmentStatus.NotFound => NotFound(),
            RescheduleAppointmentStatus.Forbidden => Forbid(),
            RescheduleAppointmentStatus.Conflict => Conflict(BuildProblem(result.Errors)),
            RescheduleAppointmentStatus.EmployeeNotFound => NotFound(BuildProblem(result.Errors)),
            RescheduleAppointmentStatus.DependencyUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, BuildProblem(result.Errors)),
            _ => ValidationProblem(BuildValidationProblem(result.Errors)),
        };
    }

    /// <summary>Storniert einen Termin (Soft-Delete, Status <c>Cancelled</c>). <c>403</c> bei Fremdzugriff ohne Admin-Rolle.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> Cancel(
        Guid id, [FromBody] CancelAppointmentRequest request, CancellationToken cancellationToken)
    {
        var command = new CancelAppointmentCommand(
            id, request.Reason, GetActorId(), IsAdmin(), GetCurrentEmployeeId());

        var result = await messageBus.InvokeAsync<CancelAppointmentResult>(command, cancellationToken);

        return result.Status switch
        {
            CancelAppointmentStatus.Success => Ok(result.Appointment),
            CancelAppointmentStatus.NotFound => NotFound(),
            CancelAppointmentStatus.Forbidden => Forbid(),
            _ => ValidationProblem(BuildValidationProblem(result.Errors)),
        };
    }

    private bool IsAdmin() => User.IsInRole("Admin");

    private Guid? GetCurrentEmployeeId() =>
        Guid.TryParse(GetActorId(), out var employeeId) ? employeeId : null;

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

    private static object BuildProblem(IReadOnlyList<string> errors) => new { errors };
}

public sealed record CreateAppointmentRequest(
    Guid CustomerId,
    Guid EmployeeId,
    Guid ServiceId,
    DateTimeOffset Start,
    DateTimeOffset End);

public sealed record RescheduleAppointmentRequest(DateTimeOffset NewStart, DateTimeOffset NewEnd);

public sealed record CancelAppointmentRequest(string Reason);
