using AppointmentService.Application.Abstractions;

namespace AppointmentService.Application.Appointments.Calendar;

public static class GetCalendarHandler
{
    public static async Task<GetCalendarResult> Handle(
        GetCalendarQuery query,
        IAppointmentRepository repository,
        CancellationToken cancellationToken)
    {
        var appointments = await repository.SearchAsync(
            query.EmployeeId, query.CustomerId, query.From, query.To, includeCancelled: false, cancellationToken);

        var items = appointments
            .OrderBy(a => a.Start)
            .Select(AppointmentListItemDto.FromDomain)
            .ToList();

        return new GetCalendarResult(items);
    }
}
