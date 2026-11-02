using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees;

public sealed record EmployeeServiceAssignmentDto(Guid ServiceId, string ServiceName);

public sealed record WorkingHoursSlotDto(DayOfWeek Weekday, TimeOnly StartTime, TimeOnly EndTime);

public sealed record AbsenceDto(
    Guid Id,
    AbsenceType Type,
    DateOnly From,
    DateOnly To,
    AbsenceStatus Status,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    string? DecidedBy,
    DateTimeOffset? DecidedAt)
{
    public static AbsenceDto FromDomain(Absence absence) => new(
        absence.Id,
        absence.Type,
        absence.From,
        absence.To,
        absence.Status,
        absence.RequestedBy,
        absence.RequestedAt,
        absence.DecidedBy,
        absence.DecidedAt);
}

public sealed record EmployeeDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    EmployeeRole Role,
    bool IsActive,
    IReadOnlyList<EmployeeServiceAssignmentDto> Services,
    IReadOnlyList<WorkingHoursSlotDto> WorkingHours,
    IReadOnlyList<AbsenceDto> Absences,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy)
{
    public static EmployeeDto FromDomain(Employee employee) => new(
        employee.Id,
        employee.FirstName,
        employee.LastName,
        employee.Email,
        employee.Role,
        employee.IsActive,
        employee.Services.Select(s => new EmployeeServiceAssignmentDto(s.ServiceId, s.ServiceName)).ToList(),
        employee.WorkingHours.Select(w => new WorkingHoursSlotDto(w.Weekday, w.StartTime, w.EndTime)).ToList(),
        employee.Absences.Select(AbsenceDto.FromDomain).ToList(),
        employee.CreatedAt,
        employee.CreatedBy,
        employee.ModifiedAt,
        employee.ModifiedBy);
}
