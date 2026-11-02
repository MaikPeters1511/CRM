namespace EmployeeService.Domain.Employees;

/// <summary>Domain Event: ein Admin hat eine ausstehende Abwesenheitsanfrage abgelehnt.</summary>
public sealed record AbsenceRejected(
    Guid EmployeeId,
    Guid AbsenceId,
    string RequestedBy,
    string RejectedBy,
    DateTimeOffset RejectedAt);
