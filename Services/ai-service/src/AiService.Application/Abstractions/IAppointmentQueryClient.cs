namespace AiService.Application.Abstractions;

/// <summary>
/// Synchroner, lesender Aufruf gegen <c>appointment-service</c> (ki-terminassistent.openspec.md,
/// Abschnitt 5): <c>GET /api/appointments?employeeId=&amp;from=&amp;to=</c>, rollenbasiert ueber den
/// weitergeleiteten Authorization-Header gefiltert (<c>AuthHeaderForwardingHandler</c>-Muster).
/// Dient der harten, LIVE-Kollisionspruefung - niemals der Qdrant-Index (docs/adr/0003, Abschnitt 4).
/// </summary>
public interface IAppointmentQueryClient
{
    Task<IReadOnlyList<BookedSlotDto>> GetBookedSlotsAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public sealed record BookedSlotDto(DateTimeOffset Start, DateTimeOffset End);
