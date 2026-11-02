namespace AppointmentService.Application.Appointments.Calendar;

/// <summary>
/// Backs <c>GET /api/appointments?employeeId=&amp;customerId=&amp;from=&amp;to=</c>
/// (terminverwaltung.openspec.md, Abschnitt 5). This is also the exact contract consumed
/// synchronously by employee-service's <c>IAppointmentAvailabilityClient</c> (ADR 0001,
/// Abschnitt 2) - see <see cref="AppointmentListItemDto"/> for details.
/// </summary>
/// <summary>
/// <paramref name="IsAdmin"/>/<paramref name="CurrentEmployeeId"/> carry the caller's authorization
/// context (terminverwaltung.openspec.md, Abschnitt 3 "Sichtbarkeit von Terminen nach Rolle (RBAC)").
/// For role <c>Employee</c> the handler SHALL ignore <paramref name="EmployeeId"/> as supplied by the
/// client and restrict the result to <paramref name="CurrentEmployeeId"/> instead - client input is
/// never trusted for authorization decisions.
/// </summary>
public sealed record GetCalendarQuery(
    Guid? EmployeeId,
    Guid? CustomerId,
    DateOnly From,
    DateOnly To,
    bool IsAdmin,
    Guid? CurrentEmployeeId);

/// <summary>
/// Design-Entscheidung (abwesenheit-workflow.openspec.md, Abschnitt 5/7): genehmigte Abwesenheiten
/// werden als eigenes, nicht-persistiertes Feld <see cref="AbsenceEntries"/> zurueckgegeben statt
/// sie in <see cref="Appointments"/> zu mischen. Grund: employee-service's bestehender
/// <c>AppointmentAvailabilityHttpClient</c> (Konfliktpruefung bei neuen Abwesenheiten/Terminen)
/// konsumiert exakt dieselbe Appointments-Liste und wuerde eine beigemischte Abwesenheit
/// faelschlich als blockierenden Termin interpretieren.
/// </summary>
public sealed record GetCalendarResult(
    IReadOnlyList<AppointmentListItemDto> Appointments,
    IReadOnlyList<CalendarAbsenceEntryDto> AbsenceEntries);

/// <summary>Ganztages-"Abwesend"-Eintrag fuer den Kalender (nicht persistiert, siehe <see cref="GetCalendarResult"/>).</summary>
public sealed record CalendarAbsenceEntryDto(Guid EmployeeId, string Type, DateOnly From, DateOnly To);
