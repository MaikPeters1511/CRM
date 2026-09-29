namespace AppointmentService.Application.Abstractions;

/// <summary>
/// Synchronous outbound contract towards <c>employee-service</c> (ADR 0001, Abschnitt 2 &amp; 5):
/// <c>GET /api/employees/{id}/availability?from=&amp;to=</c>. Mirrors the response shape of
/// <c>EmployeeService.Application.Employees.Availability.AvailableSlotDto</c> (Date/StartTime/EndTime)
/// exactly, so this client can be implemented as a thin HTTP call without any translation layer.
/// Implemented with Polly resilience (see AppointmentService.Infrastructure.DependencyInjection);
/// on dependency failure the implementation SHALL let the exception propagate.
/// </summary>
public interface IEmployeeAvailabilityClient
{
    Task<EmployeeAvailabilityLookupResult> GetAvailabilityAsync(
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}

public sealed record AvailableSlotDto(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);

public sealed record EmployeeAvailabilityLookupResult(bool Found, IReadOnlyList<AvailableSlotDto> Slots)
{
    public static EmployeeAvailabilityLookupResult NotFound() => new(false, []);

    public static EmployeeAvailabilityLookupResult Of(IReadOnlyList<AvailableSlotDto> slots) => new(true, slots);
}
