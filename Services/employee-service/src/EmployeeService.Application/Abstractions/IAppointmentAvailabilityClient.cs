namespace EmployeeService.Application.Abstractions;

/// <summary>
/// Synchronous outbound contract towards <c>appointment-service</c> (ADR 0001, Abschnitt 2 &amp; 5).
/// Used to resolve existing appointments of an employee for a given period, both for the
/// absence conflict check ("Urlaub kollidiert mit bestehendem Termin") and for the
/// availability calculation (working hours - absences - existing appointments).
///
/// KNOWN GAP: <c>appointment-service</c> does not exist yet, so the exact response contract
/// (DTO shape, route) is not finalized. This interface intentionally models the minimal
/// contract described in the spec ("Liste bestehender Termine im Zeitraum X-Y für
/// Mitarbeiter Z") so the calling code compiles and behaves correctly against any HTTP
/// implementation that honours this shape once appointment-service is available.
/// </summary>
public interface IAppointmentAvailabilityClient
{
    Task<IReadOnlyList<ExistingAppointmentSlot>> GetAppointmentsAsync(
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}

public sealed record ExistingAppointmentSlot(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);
