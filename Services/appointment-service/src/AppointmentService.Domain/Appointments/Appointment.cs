using AppointmentService.Domain.Common;

namespace AppointmentService.Domain.Appointments;

/// <summary>
/// Aggregate root for the "Appointment Scheduling" bounded context (terminverwaltung.openspec.md,
/// Abschnitt 7 / ADR 0001 Abschnitt 6). Holds the collision-check and working-hours invariants.
/// Availability/collision data is always passed in as an already-resolved snapshot
/// (<see cref="AvailabilityWindow"/>/<see cref="ExistingAppointmentSlot"/>) - this class has
/// zero dependency on HTTP clients, EF Core or messaging infrastructure.
/// </summary>
public sealed class Appointment : AggregateRoot
{
    public Guid CustomerId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid ServiceId { get; private set; }
    public DateTimeOffset Start { get; private set; }
    public DateTimeOffset End { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset? ReminderSentAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private Appointment()
    {
    }

    private Appointment(
        Guid id,
        Guid customerId,
        Guid employeeId,
        Guid serviceId,
        DateTimeOffset start,
        DateTimeOffset end,
        string createdBy,
        DateTimeOffset createdAt) : base(id)
    {
        CustomerId = customerId;
        EmployeeId = employeeId;
        ServiceId = serviceId;
        Start = start;
        End = end;
        Status = AppointmentStatus.Confirmed;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public static AppointmentMutationResult Create(
        Guid customerId,
        Guid employeeId,
        Guid serviceId,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<AvailabilityWindow> availableWindows,
        IReadOnlyList<ExistingAppointmentSlot> existingAppointments,
        string createdBy,
        DateTimeOffset createdAt)
    {
        var (errors, reason) = Validate(start, end, availableWindows, existingAppointments, excludeAppointmentId: null);
        if (errors.Count > 0)
        {
            return AppointmentMutationResult.Failure(errors, reason);
        }

        var appointment = new Appointment(Guid.NewGuid(), customerId, employeeId, serviceId, start, end, createdBy, createdAt);

        appointment.AddDomainEvent(new AppointmentCreated(
            appointment.Id, customerId, employeeId, serviceId, start, end, createdAt));

        return AppointmentMutationResult.Success(appointment);
    }

    public AppointmentMutationResult Reschedule(
        DateTimeOffset newStart,
        DateTimeOffset newEnd,
        IReadOnlyList<AvailabilityWindow> availableWindows,
        IReadOnlyList<ExistingAppointmentSlot> existingAppointments,
        string modifiedBy,
        DateTimeOffset modifiedAt)
    {
        if (Status is not (AppointmentStatus.Confirmed or AppointmentStatus.RequiresReview))
        {
            return AppointmentMutationResult.Failure(
                ["Only confirmed appointments can be rescheduled."], AppointmentMutationFailureReason.Validation);
        }

        var (errors, reason) = Validate(newStart, newEnd, availableWindows, existingAppointments, excludeAppointmentId: Id);
        if (errors.Count > 0)
        {
            return AppointmentMutationResult.Failure(errors, reason);
        }

        var oldStart = Start;
        var oldEnd = End;

        Start = newStart;
        End = newEnd;
        Status = AppointmentStatus.Confirmed;
        ModifiedBy = modifiedBy;
        ModifiedAt = modifiedAt;

        AddDomainEvent(new AppointmentRescheduled(Id, oldStart, oldEnd, newStart, newEnd, modifiedAt));

        return AppointmentMutationResult.Success(this);
    }

    public AppointmentMutationResult Cancel(string reason, string cancelledBy, DateTimeOffset cancelledAt)
    {
        if (Status == AppointmentStatus.Cancelled)
        {
            return AppointmentMutationResult.Failure(
                ["Appointment is already cancelled."], AppointmentMutationFailureReason.Validation);
        }

        if (Status == AppointmentStatus.Completed)
        {
            return AppointmentMutationResult.Failure(
                ["Completed appointments cannot be cancelled."], AppointmentMutationFailureReason.Validation);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return AppointmentMutationResult.Failure(
                ["A cancellation reason is required."], AppointmentMutationFailureReason.Validation);
        }

        Status = AppointmentStatus.Cancelled;
        CancellationReason = reason.Trim();
        ModifiedBy = cancelledBy;
        ModifiedAt = cancelledAt;

        AddDomainEvent(new AppointmentCancelled(Id, CancellationReason, cancelledAt, cancelledBy));

        return AppointmentMutationResult.Success(this);
    }

    /// <summary>
    /// Compensation for the "customer deactivated after appointment creation" race condition
    /// (ADR 0001, Abschnitt 3): marks a still-future, still-confirmed appointment for manual
    /// review instead of silently cancelling it. No-op for past/already-cancelled/completed
    /// appointments or appointments already under review.
    /// </summary>
    public void MarkRequiresReview(DateTimeOffset asOf)
    {
        if (Status != AppointmentStatus.Confirmed)
        {
            return;
        }

        if (Start <= asOf)
        {
            return;
        }

        Status = AppointmentStatus.RequiresReview;
        ModifiedAt = asOf;
    }

    public void MarkReminderSent(DateTimeOffset sentAt)
    {
        ReminderSentAt = sentAt;
    }

    private static (List<string> Errors, AppointmentMutationFailureReason Reason) Validate(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<AvailabilityWindow> availableWindows,
        IReadOnlyList<ExistingAppointmentSlot> existingAppointments,
        Guid? excludeAppointmentId)
    {
        var validationErrors = new List<string>();

        if (start >= end)
        {
            validationErrors.Add("Start must be before End.");
        }

        if (validationErrors.Count == 0 && !availableWindows.Any(w => start >= w.Start && end <= w.End))
        {
            validationErrors.Add("The requested time window is outside the employee's available working hours.");
        }

        if (validationErrors.Count > 0)
        {
            return (validationErrors, AppointmentMutationFailureReason.Validation);
        }

        var hasConflict = existingAppointments.Any(existing =>
            existing.AppointmentId != excludeAppointmentId && Overlaps(existing.Start, existing.End, start, end));

        if (hasConflict)
        {
            return (["The employee already has a conflicting appointment in this time window."],
                AppointmentMutationFailureReason.Conflict);
        }

        return ([], AppointmentMutationFailureReason.None);
    }

    private static bool Overlaps(DateTimeOffset start1, DateTimeOffset end1, DateTimeOffset start2, DateTimeOffset end2) =>
        start1 < end2 && start2 < end1;
}
