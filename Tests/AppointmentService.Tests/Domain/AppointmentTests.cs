using AppointmentService.Domain.Appointments;

namespace AppointmentService.Tests.Domain;

public class AppointmentTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid EmployeeId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a working-hours window on the given midnight-anchored <paramref name="date"/>.</summary>
    private static AvailabilityWindow WorkingDay(DateTimeOffset date, int startHour, int endHour) =>
        new(date.AddHours(startHour), date.AddHours(endHour));

    [Test]
    public async Task Create_WithinAvailableWindowAndNoConflicts_ReturnsConfirmedAppointmentWithCreatedEvent()
    {
        var day = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var start = day.AddHours(10);
        var end = day.AddHours(11);

        var result = Appointment.Create(
            CustomerId, EmployeeId, ServiceId, start, end,
            [WorkingDay(day, 9, 17)], [], "employee-1", Now);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Appointment!.Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(result.Appointment.DomainEvents).Count().IsEqualTo(1);
        await Assert.That(result.Appointment.DomainEvents.Single()).IsTypeOf<AppointmentCreated>();
    }

    [Test]
    public async Task Create_OutsideAvailableWorkingHours_ReturnsValidationFailureAndNoAppointment()
    {
        var day = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var start = day.AddHours(18);
        var end = day.AddHours(19);

        var result = Appointment.Create(
            CustomerId, EmployeeId, ServiceId, start, end,
            [WorkingDay(day, 9, 17)], [], "employee-1", Now);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Reason).IsEqualTo(AppointmentMutationFailureReason.Validation);
        await Assert.That(result.Appointment).IsNull();
    }

    [Test]
    public async Task Create_OverlappingExistingAppointment_ReturnsConflictAndNoAppointment()
    {
        var day = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var existing = new ExistingAppointmentSlot(Guid.NewGuid(), day.AddHours(10), day.AddHours(11));

        var result = Appointment.Create(
            CustomerId, EmployeeId, ServiceId, day.AddHours(10).AddMinutes(30), day.AddHours(11).AddMinutes(30),
            [WorkingDay(day, 9, 17)], [existing], "employee-1", Now);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Reason).IsEqualTo(AppointmentMutationFailureReason.Conflict);
    }

    [Test]
    public async Task Create_WithStartNotBeforeEnd_ReturnsValidationFailure()
    {
        var day = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

        var result = Appointment.Create(
            CustomerId, EmployeeId, ServiceId, day.AddHours(11), day.AddHours(10),
            [WorkingDay(day, 9, 17)], [], "employee-1", Now);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Reason).IsEqualTo(AppointmentMutationFailureReason.Validation);
    }

    [Test]
    public async Task Reschedule_ToKollisionsfreiesZeitfenster_UpdatesAppointmentAndPublishesRescheduledEvent()
    {
        var day = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var appointment = Appointment.Create(
            CustomerId, EmployeeId, ServiceId, day.AddHours(10), day.AddHours(11),
            [WorkingDay(day, 9, 17)], [], "employee-1", Now).Appointment!;
        appointment.ClearDomainEvents();

        var result = appointment.Reschedule(
            day.AddHours(13), day.AddHours(14), [WorkingDay(day, 9, 17)], [], "employee-1", Now);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(appointment.Start).IsEqualTo(day.AddHours(13));
        await Assert.That(appointment.End).IsEqualTo(day.AddHours(14));
        await Assert.That(appointment.DomainEvents.Single()).IsTypeOf<AppointmentRescheduled>();
    }

    [Test]
    public async Task Reschedule_IntoSlotOccupiedByAnotherAppointment_ReturnsConflict()
    {
        var day = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var appointment = Appointment.Create(
            CustomerId, EmployeeId, ServiceId, day.AddHours(10), day.AddHours(11),
            [WorkingDay(day, 9, 17)], [], "employee-1", Now).Appointment!;

        var otherExisting = new ExistingAppointmentSlot(Guid.NewGuid(), day.AddHours(13), day.AddHours(14));

        var result = appointment.Reschedule(
            day.AddHours(13).AddMinutes(30), day.AddHours(14).AddMinutes(30), [WorkingDay(day, 9, 17)], [otherExisting], "employee-1", Now);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Reason).IsEqualTo(AppointmentMutationFailureReason.Conflict);
    }

    [Test]
    public async Task Cancel_WithReason_MarksCancelledAndPublishesCancelledEvent()
    {
        var day = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var appointment = Appointment.Create(
            CustomerId, EmployeeId, ServiceId, day.AddHours(10), day.AddHours(11),
            [WorkingDay(day, 9, 17)], [], "employee-1", Now).Appointment!;
        appointment.ClearDomainEvents();

        var result = appointment.Cancel("Kunde krank", "employee-1", Now);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(appointment.Status).IsEqualTo(AppointmentStatus.Cancelled);
        await Assert.That(appointment.CancellationReason).IsEqualTo("Kunde krank");
        await Assert.That(appointment.DomainEvents.Single()).IsTypeOf<AppointmentCancelled>();
    }

    [Test]
    public async Task Cancel_WithoutReason_ReturnsValidationFailure()
    {
        var day = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var appointment = Appointment.Create(
            CustomerId, EmployeeId, ServiceId, day.AddHours(10), day.AddHours(11),
            [WorkingDay(day, 9, 17)], [], "employee-1", Now).Appointment!;

        var result = appointment.Cancel("   ", "employee-1", Now);

        await Assert.That(result.IsSuccess).IsFalse();
    }

    [Test]
    public async Task Cancel_AlreadyCancelledAppointment_ReturnsValidationFailure()
    {
        var day = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var appointment = Appointment.Create(
            CustomerId, EmployeeId, ServiceId, day.AddHours(10), day.AddHours(11),
            [WorkingDay(day, 9, 17)], [], "employee-1", Now).Appointment!;
        appointment.Cancel("erster Grund", "employee-1", Now);

        var result = appointment.Cancel("zweiter Grund", "employee-1", Now);

        await Assert.That(result.IsSuccess).IsFalse();
    }

    [Test]
    public async Task MarkRequiresReview_OnFutureConfirmedAppointment_ChangesStatus()
    {
        var future = Now.AddDays(5);
        var appointment = Appointment.Create(
            CustomerId, EmployeeId, ServiceId, future.AddHours(10), future.AddHours(11),
            [WorkingDay(future, 9, 17)], [], "employee-1", Now).Appointment!;

        appointment.MarkRequiresReview(Now);

        await Assert.That(appointment.Status).IsEqualTo(AppointmentStatus.RequiresReview);
    }

    [Test]
    public async Task MarkRequiresReview_OnPastAppointment_DoesNotChangeStatus()
    {
        var past = Now.AddDays(-5);
        var appointment = Appointment.Create(
            CustomerId, EmployeeId, ServiceId, past.AddHours(10), past.AddHours(11),
            [WorkingDay(past, 9, 17)], [], "employee-1", past.AddDays(-1)).Appointment!;

        appointment.MarkRequiresReview(Now);

        await Assert.That(appointment.Status).IsEqualTo(AppointmentStatus.Confirmed);
    }
}
