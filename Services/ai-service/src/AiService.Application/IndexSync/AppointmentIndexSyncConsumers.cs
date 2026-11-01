using AiService.Application.Abstractions;

namespace AiService.Application.IndexSync;

/// <summary>
/// Haelt den Qdrant-Verfuegbarkeitsindex bei Terminaenderungen aktuell (docs/adr/0003, Abschnitt 2).
/// Idempotenz: Markieren eines bereits markierten Fensters ist ein No-Op fuer den fachlichen Ausgang
/// (reine Ableitungsdaten, letztgueltige Aktualisierung pro Mitarbeiter/Zeitfenster - ADR Abschnitt 6).
/// KNOWN LIMITATION fuer Cancelled/Rescheduled: die Integration Events von appointment-service
/// tragen bewusst keine EmployeeId (siehe AppointmentCancelledIntegrationEvent dort) - eine
/// Live-Nachfrage via GET /api/appointments/{id} wuerde in diesem Hintergrund-Konsumenten ohne
/// eingehenden HTTP-Request-Kontext am AuthHeaderForwardingHandler-Grenzfall scheitern (401, siehe
/// AppointmentService.Infrastructure.Clients.AuthHeaderForwardingHandler, "KNOWN LIMITATION").
/// Fehlende Praezision hier ist laut ADR 0003 Abschnitt 4 fachlich irrelevant (Qdrant ist nie
/// alleinige Entscheidungsgrundlage) und wird vom taeglichen Konsistenz-Sync (Abschnitt 3)
/// repariert - kein Gold-Plating durch einen verfruehten Service-Account-Mechanismus.
/// </summary>
public static class AppointmentIndexSyncConsumers
{
    public static Task Handle(
        AppointmentCreatedIntegrationEvent @event, IAvailabilityIndexClient indexClient, CancellationToken cancellationToken) =>
        indexClient.MarkBusyAsync(@event.EmployeeId, @event.Start, @event.End, cancellationToken);

    public static Task Handle(AppointmentCancelledIntegrationEvent @event, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public static Task Handle(AppointmentRescheduledIntegrationEvent @event, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
