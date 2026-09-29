using System.Security.Claims;
using EmployeeService.Application.Employees;
using EmployeeService.Application.Employees.AddAbsence;
using EmployeeService.Application.Employees.Availability;
using EmployeeService.Application.Employees.Create;
using EmployeeService.Application.Employees.Deactivate;
using EmployeeService.Application.Employees.GetById;
using EmployeeService.Application.Employees.Search;
using EmployeeService.Application.Employees.SetWorkingHours;
using EmployeeService.Application.Employees.Update;
using EmployeeService.Domain.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Wolverine;

namespace EmployeeService.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(IMessageBus messageBus) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<ActionResult<SearchEmployeesResult>> Search(
        [FromQuery] string? search,
        [FromQuery] EmployeeRole? role,
        [FromQuery] Guid? serviceId,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await messageBus.InvokeAsync<SearchEmployeesResult>(
            new SearchEmployeesQuery(search, role, serviceId, page), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await messageBus.InvokeAsync<GetEmployeeByIdResult>(
            new GetEmployeeByIdQuery(id), cancellationToken);

        return result.Found ? Ok(result.Employee) : NotFound();
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateEmployeeCommand(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Role,
            GetActorId());

        var result = await messageBus.InvokeAsync<CreateEmployeeResult>(command, cancellationToken);

        return result.Status switch
        {
            CreateEmployeeStatus.Success => CreatedAtAction(nameof(GetById), new { id = result.Employee!.Id }, result.Employee),
            CreateEmployeeStatus.EmailConflict => Conflict(BuildProblem(result.Errors)),
            _ => ValidationProblem(BuildValidationProblem(result.Errors)),
        };
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEmployeeCommand(
            id,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Role,
            request.Services,
            GetActorId());

        var result = await messageBus.InvokeAsync<UpdateEmployeeResult>(command, cancellationToken);

        return result.Status switch
        {
            UpdateEmployeeStatus.Success => Ok(result.Employee),
            UpdateEmployeeStatus.NotFound => NotFound(),
            _ => ValidationProblem(BuildValidationProblem(result.Errors)),
        };
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeactivateEmployeeCommand(id, GetActorId());

        var result = await messageBus.InvokeAsync<DeactivateEmployeeResult>(command, cancellationToken);

        return result.Status switch
        {
            DeactivateEmployeeStatus.Success => NoContent(),
            _ => NotFound(),
        };
    }

    [HttpPut("{id:guid}/schedule")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetSchedule(
        Guid id,
        [FromBody] SetWorkingHoursRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SetWorkingHoursCommand(id, request.Slots, GetActorId());

        var result = await messageBus.InvokeAsync<SetWorkingHoursResult>(command, cancellationToken);

        return result.Status switch
        {
            SetWorkingHoursStatus.Success => Ok(result.Employee),
            SetWorkingHoursStatus.NotFound => NotFound(),
            _ => ValidationProblem(BuildValidationProblem(result.Errors)),
        };
    }

    [HttpPost("{id:guid}/absences")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> AddAbsence(
        Guid id,
        [FromBody] AddAbsenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin() && !IsOwnEmployeeRecord(id))
        {
            return Forbid();
        }

        var command = new AddAbsenceCommand(id, request.Type, request.From, request.To, GetActorId(), request.ConfirmOverride);

        var result = await messageBus.InvokeAsync<AddAbsenceResult>(command, cancellationToken);

        return result.Status switch
        {
            AddAbsenceStatus.Success => CreatedAtAction(nameof(GetById), new { id }, result.Absence),
            AddAbsenceStatus.NotFound => NotFound(),
            AddAbsenceStatus.Conflict => Conflict(new
            {
                errors = result.Errors,
                conflictingAppointments = result.ConflictingAppointments,
            }),
            _ => ValidationProblem(BuildValidationProblem(result.Errors)),
        };
    }

    [HttpGet("{id:guid}/availability")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> GetAvailability(
        Guid id,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var result = await messageBus.InvokeAsync<GetEmployeeAvailabilityResult>(
            new GetEmployeeAvailabilityQuery(id, from, to), cancellationToken);

        return result.Found ? Ok(result.Slots) : NotFound();
    }

    private bool IsAdmin() => User.IsInRole("Admin");

    private bool IsOwnEmployeeRecord(Guid employeeId) =>
        Guid.TryParse(GetActorId(), out var actorId) && actorId == employeeId;

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

public sealed record CreateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    EmployeeRole Role);

public sealed record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    EmployeeRole Role,
    IReadOnlyList<EmployeeServiceAssignmentDto>? Services);

public sealed record SetWorkingHoursRequest(IReadOnlyList<WorkingHoursSlotDto> Slots);

public sealed record AddAbsenceRequest(
    AbsenceType Type,
    DateOnly From,
    DateOnly To,
    bool ConfirmOverride = false);
