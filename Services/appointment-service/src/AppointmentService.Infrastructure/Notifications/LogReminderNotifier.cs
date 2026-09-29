using AppointmentService.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace AppointmentService.Infrastructure.Notifications;

/// <summary>
/// KNOWN SIMPLIFICATION (terminverwaltung.openspec.md, Abschnitt 3 "Terminerinnerung"): there is
/// no e-mail/notification service anywhere in this project yet, so reminders are only logged here
/// instead of actually sent by e-mail/SMTP. Swapping in a real notification service later only
/// requires a new <see cref="IReminderNotifier"/> implementation - no caller changes.
/// </summary>
public sealed class LogReminderNotifier(ILogger<LogReminderNotifier> logger) : IReminderNotifier
{
    public Task NotifyAsync(AppointmentReminderDto reminder, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Reminder (STUB, no real e-mail sent): Appointment {AppointmentId} for Customer {CustomerId} " +
            "with Employee {EmployeeId} starts at {Start} (ends {End}).",
            reminder.AppointmentId, reminder.CustomerId, reminder.EmployeeId, reminder.Start, reminder.End);

        return Task.CompletedTask;
    }
}
