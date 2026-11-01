namespace AiService.Application.Abstractions;

/// <summary>
/// Abstraktion ueber den Qdrant-Verfuegbarkeitsindex (docs/adr/0003-ai-service-qdrant-index-strategie.md).
/// Dient AUSSCHLIESSLICH der semantischen Kandidaten-Vorauswahl (z.B. "Farbe + Schnitt" -&gt;
/// passende Katalog-Leistungen trotz Synonymen/Tippfehlern) - niemals der harten
/// Verfuegbarkeitsentscheidung (das bleibt <see cref="IAppointmentQueryClient"/>/
/// <see cref="IEmployeeQueryClient"/> vorbehalten, ADR 0003 Abschnitt 4). Enthaelt laut ADR
/// ausschliesslich abstrahierte, aggregierte Daten - keine Kunden-/Terminhistorie.
/// </summary>
public interface IAvailabilityIndexClient
{
    Task UpsertServiceAsync(Guid serviceId, string name, string? description, bool isActive, CancellationToken cancellationToken);

    Task RemoveServiceAsync(Guid serviceId, CancellationToken cancellationToken);

    Task UpsertEmployeeAsync(
        Guid employeeId, string displayName, IReadOnlyList<Guid> serviceIds, CancellationToken cancellationToken);

    Task RemoveEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);

    /// <summary>Grobe Belegungs-Heuristik (ADR 0003 Abschnitt 1) - KEIN Ersatz fuer die Live-Kollisionspruefung.</summary>
    Task MarkBusyAsync(Guid employeeId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken);

    Task MarkFreeAsync(Guid employeeId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken);

    /// <summary>Semantische Vorauswahl passender Leistungs-Ids fuer einen Freitext-Begriff.</summary>
    Task<IReadOnlyList<Guid>> FindCandidateServiceIdsAsync(string queryText, CancellationToken cancellationToken);
}

public sealed class AvailabilityIndexUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
