using AppointmentService.Application.Abstractions;

namespace AppointmentService.Application.Appointments.CustomerDeactivated;

/// <summary>
/// Compensation logic for the race condition described in ADR 0001, Abschnitt 3: if a customer is
/// deactivated AFTER an appointment was already confirmed (the synchronous precondition check in
/// <c>CreateAppointmentHandler</c> ran before the deactivation), this consumer marks the affected
/// FUTURE, still-confirmed appointments as <see cref="Domain.Appointments.AppointmentStatus.RequiresReview"/>
/// instead of auto-cancelling them - a human decides manually (no silent data loss/no automatic
/// cancellation). This handler is naturally idempotent because re-processing the same event on an
/// already-`RequiresReview` (or already past/cancelled/completed) appointment is a no-op
/// (see <c>Appointment.MarkRequiresReview</c>) - safe even without a durable Wolverine
/// Inbox/Outbox, which this project does not configure yet (KNOWN SIMPLIFICATION, consistent with
/// customer-service/employee-service).
/// </summary>
public static class CustomerDeactivatedConsumer
{
    public static async Task Handle(
        CustomerDeactivatedIntegrationEvent @event,
        IAppointmentRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var futureAppointments = await repository.GetFutureConfirmedByCustomerAsync(
            @event.CustomerId, now, cancellationToken);

        foreach (var appointment in futureAppointments)
        {
            appointment.MarkRequiresReview(now);
        }

        if (futureAppointments.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
    }
}
