using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees.AddAbsence;

public sealed record AddAbsenceCommand(
    Guid EmployeeId,
    AbsenceType Type,
    DateOnly From,
    DateOnly To,
    string AddedBy,
    bool ConfirmOverride = false);

public enum AddAbsenceStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Conflict
}

public sealed record AddAbsenceResult(
    AddAbsenceStatus Status,
    AbsenceDto? Absence,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ExistingAppointmentSlot> ConflictingAppointments)
{
    public static AddAbsenceResult Success(AbsenceDto absence) =>
        new(AddAbsenceStatus.Success, absence, [], []);

    public static AddAbsenceResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(AddAbsenceStatus.ValidationFailed, null, errors, []);

    public static AddAbsenceResult NotFound() =>
        new(AddAbsenceStatus.NotFound, null, [], []);

    public static AddAbsenceResult Conflict(IReadOnlyList<ExistingAppointmentSlot> conflictingAppointments) =>
        new(AddAbsenceStatus.Conflict,
            null,
            ["The absence period conflicts with existing appointments. Confirm explicitly or reschedule the appointments."],
            conflictingAppointments);
}
