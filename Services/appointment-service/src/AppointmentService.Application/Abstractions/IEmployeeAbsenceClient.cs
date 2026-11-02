namespace AppointmentService.Application.Abstractions;

/// <summary>
/// Synchronous outbound contract towards <c>employee-service</c> (ADR 0001, Abschnitt 2 &amp; 5):
/// <c>GET /api/employees/absences/approved?from=&amp;to=&amp;employeeId=</c>
/// (abwesenheit-workflow.openspec.md, Abschnitt 5). Used by <c>GetCalendarHandler</c> to augment
/// the calendar response with approved, employee-wide absences as separate, non-persisted
/// all-day entries. Implemented with Polly resilience (see
/// AppointmentService.Infrastructure.DependencyInjection); on dependency failure the
/// implementation SHALL let the exception propagate (identical contract to
/// <see cref="IEmployeeAvailabilityClient"/>).
/// </summary>
public interface IEmployeeAbsenceClient
{
    Task<IReadOnlyList<ApprovedAbsenceDto>> GetApprovedAbsencesAsync(
        Guid? employeeId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}

/// <summary>
/// Mirrors employee-service's <c>ApprovedAbsenceDto</c> response shape. <see cref="Type"/> is
/// forwarded as a raw string (not a shared enum) to avoid coupling the two bounded contexts to a
/// shared type definition - appointment-service only needs it for display purposes.
/// </summary>
public sealed record ApprovedAbsenceDto(Guid EmployeeId, string Type, DateOnly From, DateOnly To);
