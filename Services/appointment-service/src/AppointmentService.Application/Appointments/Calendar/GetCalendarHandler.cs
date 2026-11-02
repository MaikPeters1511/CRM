using AppointmentService.Application.Abstractions;

namespace AppointmentService.Application.Appointments.Calendar;

public static class GetCalendarHandler
{
    public static async Task<GetCalendarResult> Handle(
        GetCalendarQuery query,
        IAppointmentRepository repository,
        IEmployeeAbsenceClient employeeAbsenceClient,
        CancellationToken cancellationToken)
    {
        // RBAC (terminverwaltung.openspec.md, Abschnitt 3): Employee-Rolle sieht ausschliesslich
        // eigene Termine - der client-seitige employeeId-Filter wird dafuer nicht vertraut/verwendet.
        if (!query.IsAdmin && query.CurrentEmployeeId is null)
        {
            // Kein eindeutiger NameIdentifier-Claim vorhanden -> fail-closed, keine Termine zurueckgeben.
            return new GetCalendarResult([], []);
        }

        var effectiveEmployeeId = query.IsAdmin ? query.EmployeeId : query.CurrentEmployeeId;

        var appointments = await repository.SearchAsync(
            effectiveEmployeeId, query.CustomerId, query.From, query.To, includeCancelled: false, cancellationToken);

        var items = appointments
            .OrderBy(a => a.Start)
            .Select(AppointmentListItemDto.FromDomain)
            .ToList();

        // abwesenheit-workflow.openspec.md, Scenario "Genehmigte Abwesenheit erscheint im Kalender":
        // genehmigte Abwesenheiten werden synchron bei employee-service abgefragt und als separate,
        // nicht-persistierte Ganztages-Eintraege zurueckgegeben (siehe GetCalendarResult).
        var absences = await employeeAbsenceClient.GetApprovedAbsencesAsync(
            effectiveEmployeeId, query.From, query.To, cancellationToken);

        var absenceEntries = absences
            .OrderBy(a => a.From)
            .Select(a => new CalendarAbsenceEntryDto(a.EmployeeId, a.Type, a.From, a.To))
            .ToList();

        return new GetCalendarResult(items, absenceEntries);
    }
}
