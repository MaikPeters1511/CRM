using AppointmentService.Application.Abstractions;

namespace AppointmentService.Application.Appointments.Calendar;

public static class GetCalendarHandler
{
    public static async Task<GetCalendarResult> Handle(
        GetCalendarQuery query,
        IAppointmentRepository repository,
        CancellationToken cancellationToken)
    {
        // RBAC (terminverwaltung.openspec.md, Abschnitt 3): Employee-Rolle sieht ausschliesslich
        // eigene Termine - der client-seitige employeeId-Filter wird dafuer nicht vertraut/verwendet.
        if (!query.IsAdmin && query.CurrentEmployeeId is null)
        {
            // Kein eindeutiger NameIdentifier-Claim vorhanden -> fail-closed, keine Termine zurueckgeben.
            return new GetCalendarResult([]);
        }

        var effectiveEmployeeId = query.IsAdmin ? query.EmployeeId : query.CurrentEmployeeId;

        var appointments = await repository.SearchAsync(
            effectiveEmployeeId, query.CustomerId, query.From, query.To, includeCancelled: false, cancellationToken);

        var items = appointments
            .OrderBy(a => a.Start)
            .Select(AppointmentListItemDto.FromDomain)
            .ToList();

        return new GetCalendarResult(items);
    }
}
