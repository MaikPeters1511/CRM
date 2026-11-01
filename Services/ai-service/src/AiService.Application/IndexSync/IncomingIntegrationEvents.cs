namespace AiService.Application.IndexSync;

/// <summary>
/// Lokale Kopien der von den Quell-Services veroeffentlichten Integration Events
/// (docs/adr/0003-ai-service-qdrant-index-strategie.md, Abschnitt 2). Bewusst KEINE
/// Projektreferenz auf AppointmentService.Application/EmployeeService.Application/
/// CatalogService.Application - identisches Entkopplungsmuster wie
/// AppointmentService.Application.Appointments.CustomerDeactivated.CustomerDeactivatedIntegrationEvent
/// (eigene Kopie statt Kompilierzeit-Kopplung zwischen Services).
/// </summary>
public sealed record AppointmentCreatedIntegrationEvent(
    Guid AppointmentId, Guid CustomerId, Guid EmployeeId, Guid ServiceId,
    DateTimeOffset Start, DateTimeOffset End, DateTimeOffset CreatedAt);

public sealed record AppointmentCancelledIntegrationEvent(
    Guid AppointmentId, string Reason, DateTimeOffset CancelledAt, string CancelledBy);

public sealed record AppointmentRescheduledIntegrationEvent(
    Guid AppointmentId, DateTimeOffset OldStart, DateTimeOffset OldEnd,
    DateTimeOffset NewStart, DateTimeOffset NewEnd, DateTimeOffset RescheduledAt);

public sealed record EmployeeDeactivatedIntegrationEvent(
    Guid EmployeeId, DateTimeOffset DeactivatedAt, string DeactivatedBy);

/// <summary>Spiegelt die Reihenfolge von CatalogService.Domain.Services.ServiceCatalogChangeType (JSON-Ordinal-Kompatibilitaet).</summary>
public enum ServiceCatalogChangeType
{
    Created,
    Updated,
    Deactivated,
}

public sealed record ServiceCatalogChangedIntegrationEvent(
    Guid ServiceId, string Name, string? Description, int? DefaultDurationMinutes,
    bool IsActive, ServiceCatalogChangeType ChangeType, DateTimeOffset ChangedAt);
