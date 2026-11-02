namespace EmployeeService.Application.Employees.RejectAbsence;

/// <summary>Integration Event (Wolverine/RabbitMQ), analog AbsenceApprovedIntegrationEvent.</summary>
public sealed record AbsenceRejectedIntegrationEvent(
    Guid AbsenceId,
    Guid EmployeeId,
    string RejectedBy,
    DateTimeOffset RejectedAt);
