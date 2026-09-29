using EmployeeService.Domain.Common;

namespace EmployeeService.Domain.Employees;

public sealed class Absence : Entity
{
    public AbsenceType Type { get; private set; }
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }

    private Absence()
    {
    }

    public Absence(Guid id, AbsenceType type, DateOnly from, DateOnly to) : base(id)
    {
        if (from > to)
        {
            throw new ArgumentException("From must be on or before To.", nameof(from));
        }

        Type = type;
        From = from;
        To = to;
    }

    public bool Overlaps(DateOnly from, DateOnly to) => From <= to && To >= from;
}
