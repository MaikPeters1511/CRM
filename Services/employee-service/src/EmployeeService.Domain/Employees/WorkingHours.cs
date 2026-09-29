using EmployeeService.Domain.Common;

namespace EmployeeService.Domain.Employees;

public sealed class WorkingHours : Entity
{
    public DayOfWeek Weekday { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }

    private WorkingHours()
    {
    }

    public WorkingHours(Guid id, DayOfWeek weekday, TimeOnly startTime, TimeOnly endTime) : base(id)
    {
        if (startTime >= endTime)
        {
            throw new ArgumentException("StartTime must be before EndTime.", nameof(startTime));
        }

        Weekday = weekday;
        StartTime = startTime;
        EndTime = endTime;
    }
}
