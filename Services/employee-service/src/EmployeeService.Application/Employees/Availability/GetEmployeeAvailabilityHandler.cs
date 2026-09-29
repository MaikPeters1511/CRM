using EmployeeService.Application.Abstractions;

namespace EmployeeService.Application.Employees.Availability;

/// <summary>
/// Combines local working hours and absences with existing appointments fetched
/// synchronously from `appointment-service` (ADR 0001, Abschnitt 2/5) to compute the
/// remaining free time windows for an employee within a date range.
/// </summary>
public static class GetEmployeeAvailabilityHandler
{
    public static async Task<GetEmployeeAvailabilityResult> Handle(
        GetEmployeeAvailabilityQuery query,
        IEmployeeRepository repository,
        IAppointmentAvailabilityClient appointmentAvailabilityClient,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(query.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return GetEmployeeAvailabilityResult.NotFound();
        }

        if (!employee.IsActive || query.From > query.To)
        {
            return GetEmployeeAvailabilityResult.Success([]);
        }

        var existingAppointments = await appointmentAvailabilityClient.GetAppointmentsAsync(
            query.EmployeeId, query.From, query.To, cancellationToken);

        var appointmentsByDate = existingAppointments
            .GroupBy(a => a.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var slots = new List<AvailableSlotDto>();

        for (var date = query.From; date <= query.To; date = date.AddDays(1))
        {
            if (employee.HasAbsenceOverlap(date, date))
            {
                continue;
            }

            var windows = employee.WorkingHours
                .Where(w => w.Weekday == date.DayOfWeek)
                .Select(w => (Start: w.StartTime, End: w.EndTime))
                .ToList();

            if (windows.Count == 0)
            {
                continue;
            }

            var freeWindows = windows;

            if (appointmentsByDate.TryGetValue(date, out var appointmentsOnDate))
            {
                foreach (var appointment in appointmentsOnDate)
                {
                    freeWindows = Subtract(freeWindows, appointment.StartTime, appointment.EndTime);
                }
            }

            foreach (var (start, end) in freeWindows.OrderBy(w => w.Start))
            {
                slots.Add(new AvailableSlotDto(date, start, end));
            }
        }

        return GetEmployeeAvailabilityResult.Success(slots);
    }

    private static List<(TimeOnly Start, TimeOnly End)> Subtract(
        List<(TimeOnly Start, TimeOnly End)> freeWindows,
        TimeOnly busyStart,
        TimeOnly busyEnd)
    {
        var result = new List<(TimeOnly Start, TimeOnly End)>();

        foreach (var window in freeWindows)
        {
            if (busyEnd <= window.Start || busyStart >= window.End)
            {
                result.Add(window);
                continue;
            }

            if (busyStart > window.Start)
            {
                result.Add((window.Start, busyStart));
            }

            if (busyEnd < window.End)
            {
                result.Add((busyEnd, window.End));
            }
        }

        return result;
    }
}
