namespace EmployeeService.Domain.Employees;

/// <summary>Domain Event: ein Admin hat eine ausstehende Abwesenheitsanfrage genehmigt.</summary>
public sealed record AbsenceApproved(
    Guid EmployeeId,
    Guid AbsenceId,
    AbsenceType Type,
    DateOnly From,
    DateOnly To,
    string RequestedBy,
    string ApprovedBy,
    DateTimeOffset ApprovedAt);
