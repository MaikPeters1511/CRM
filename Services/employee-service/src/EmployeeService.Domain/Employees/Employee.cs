using EmployeeService.Domain.Common;

namespace EmployeeService.Domain.Employees;

public sealed class Employee : AggregateRoot
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public EmployeeRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private readonly List<EmployeeServiceAssignment> _services = [];
    public IReadOnlyList<EmployeeServiceAssignment> Services => _services.AsReadOnly();

    private readonly List<WorkingHours> _workingHours = [];
    public IReadOnlyList<WorkingHours> WorkingHours => _workingHours.AsReadOnly();

    private readonly List<Absence> _absences = [];
    public IReadOnlyList<Absence> Absences => _absences.AsReadOnly();

    private Employee()
    {
    }

    private Employee(
        Guid id,
        string firstName,
        string lastName,
        string email,
        EmployeeRole role,
        string createdBy,
        DateTimeOffset createdAt) : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Role = role;
        IsActive = true;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public static EmployeeMutationResult Create(
        string firstName,
        string lastName,
        string email,
        EmployeeRole role,
        string createdBy,
        DateTimeOffset createdAt)
    {
        var errors = Validate(firstName, lastName, email, role);
        if (errors.Count > 0)
        {
            return EmployeeMutationResult.Failure(errors);
        }

        var employee = new Employee(
            Guid.NewGuid(),
            firstName.Trim(),
            lastName.Trim(),
            email.Trim(),
            role,
            createdBy,
            createdAt);

        return EmployeeMutationResult.Success(employee);
    }

    public EmployeeMutationResult Update(
        string firstName,
        string lastName,
        string email,
        EmployeeRole role,
        string modifiedBy,
        DateTimeOffset modifiedAt)
    {
        var errors = Validate(firstName, lastName, email, role);
        if (errors.Count > 0)
        {
            return EmployeeMutationResult.Failure(errors);
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim();
        Role = role;
        ModifiedBy = modifiedBy;
        ModifiedAt = modifiedAt;

        return EmployeeMutationResult.Success(this);
    }

    public void AssignService(Guid serviceId, string serviceName)
    {
        if (_services.Any(s => s.ServiceId == serviceId))
        {
            return;
        }

        _services.Add(new EmployeeServiceAssignment(serviceId, serviceName));
    }

    public void ReplaceServices(IEnumerable<EmployeeServiceAssignment> services, string modifiedBy, DateTimeOffset modifiedAt)
    {
        _services.Clear();
        _services.AddRange(services);
        ModifiedBy = modifiedBy;
        ModifiedAt = modifiedAt;
    }

    public WorkingHoursMutationResult SetWorkingHours(
        IReadOnlyList<(DayOfWeek Weekday, TimeOnly StartTime, TimeOnly EndTime)> slots,
        string modifiedBy,
        DateTimeOffset modifiedAt)
    {
        var errors = new List<string>();
        foreach (var slot in slots)
        {
            if (slot.StartTime >= slot.EndTime)
            {
                errors.Add($"StartTime must be before EndTime for {slot.Weekday}.");
            }
        }

        if (errors.Count > 0)
        {
            return WorkingHoursMutationResult.Failure(errors);
        }

        _workingHours.Clear();
        foreach (var slot in slots)
        {
            _workingHours.Add(new WorkingHours(Guid.NewGuid(), slot.Weekday, slot.StartTime, slot.EndTime));
        }

        ModifiedBy = modifiedBy;
        ModifiedAt = modifiedAt;

        return WorkingHoursMutationResult.Success();
    }

    public AbsenceMutationResult AddAbsence(
        AbsenceType type,
        DateOnly from,
        DateOnly to,
        string addedBy,
        DateTimeOffset addedAt)
    {
        if (from > to)
        {
            return AbsenceMutationResult.Failure(["From must be on or before To."]);
        }

        var absence = new Absence(Guid.NewGuid(), type, from, to);
        _absences.Add(absence);
        ModifiedBy = addedBy;
        ModifiedAt = addedAt;

        return AbsenceMutationResult.Success(absence);
    }

    public bool HasAbsenceOverlap(DateOnly from, DateOnly to) => _absences.Any(a => a.Overlaps(from, to));

    public void Deactivate(string deactivatedBy, DateTimeOffset deactivatedAt)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        ModifiedBy = deactivatedBy;
        ModifiedAt = deactivatedAt;

        AddDomainEvent(new EmployeeDeactivated(Id, deactivatedAt, deactivatedBy));
    }

    private static IReadOnlyList<string> Validate(string firstName, string lastName, string email, EmployeeRole role)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(firstName))
        {
            errors.Add("FirstName is required.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            errors.Add("LastName is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add("Email is required.");
        }
        else if (!email.Contains('@', StringComparison.Ordinal))
        {
            errors.Add("Email must be a valid email address.");
        }

        if (!Enum.IsDefined(role))
        {
            errors.Add("Role is invalid.");
        }

        return errors;
    }
}
