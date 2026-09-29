using AppointmentService.Application.Appointments.CustomerDeactivated;
using AppointmentService.Domain.Appointments;
using AppointmentService.Tests.Support;

namespace AppointmentService.Tests.Application;

/// <summary>
/// ADR 0001, Abschnitt 3: Kompensationslogik bei nachtraeglicher Kundendeaktivierung -
/// zukuenftige, noch bestaetigte Termine werden auf `RequiresReview` gesetzt statt automatisch storniert.
/// </summary>
public class CustomerDeactivatedConsumerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_WithFutureConfirmedAppointmentForDeactivatedCustomer_MarksRequiresReview()
    {
        var repository = new FakeAppointmentRepository();
        var customerId = Guid.NewGuid();
        var future = Now.AddDays(5);

        var appointment = Appointment.Create(
            customerId, Guid.NewGuid(), Guid.NewGuid(), future.AddHours(10), future.AddHours(11),
            [new AvailabilityWindow(future.AddHours(9), future.AddHours(17))], [], "employee-1", Now).Appointment!;
        repository.Seed(appointment);

        var @event = new CustomerDeactivatedIntegrationEvent(customerId, Now, "admin-1");

        await CustomerDeactivatedConsumer.Handle(@event, repository, Clock, CancellationToken.None);

        await Assert.That(appointment.Status).IsEqualTo(AppointmentStatus.RequiresReview);
        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_WithPastAppointmentForDeactivatedCustomer_DoesNotChangeStatus()
    {
        var repository = new FakeAppointmentRepository();
        var customerId = Guid.NewGuid();
        var past = Now.AddDays(-5);

        var appointment = Appointment.Create(
            customerId, Guid.NewGuid(), Guid.NewGuid(), past.AddHours(10), past.AddHours(11),
            [new AvailabilityWindow(past.AddHours(9), past.AddHours(17))], [], "employee-1", past.AddDays(-1)).Appointment!;
        repository.Seed(appointment);

        var @event = new CustomerDeactivatedIntegrationEvent(customerId, Now, "admin-1");

        await CustomerDeactivatedConsumer.Handle(@event, repository, Clock, CancellationToken.None);

        await Assert.That(appointment.Status).IsEqualTo(AppointmentStatus.Confirmed);
    }

    [Test]
    public async Task Handle_WithNoAppointmentsForCustomer_DoesNotCallSaveChanges()
    {
        var repository = new FakeAppointmentRepository();
        var @event = new CustomerDeactivatedIntegrationEvent(Guid.NewGuid(), Now, "admin-1");

        await CustomerDeactivatedConsumer.Handle(@event, repository, Clock, CancellationToken.None);

        await Assert.That(repository.SaveChangesCallCount).IsEqualTo(0);
    }
}
