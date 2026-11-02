using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees.ApproveAbsence;

/// <summary>
/// Integration Event (Wolverine/RabbitMQ, analog EmployeeDeactivatedIntegrationEvent). Wird
/// innerhalb von employee-service selbst konsumiert, um die Notification anzulegen
/// (In-Process-Handler via direktem Repository-Call im Handler, kein Transport nötig dafür) -
/// das Event wird dennoch publiziert, damit zukünftige Konsumenten (z.B. appointment-service)
/// ohne Schema-Änderung andocken können (abwesenheit-workflow.openspec.md, Abschnitt 5).
/// </summary>
public sealed record AbsenceApprovedIntegrationEvent(
    Guid AbsenceId,
    Guid EmployeeId,
    DateOnly From,
    DateOnly To,
    AbsenceType Type,
    string ApprovedBy,
    DateTimeOffset ApprovedAt);
