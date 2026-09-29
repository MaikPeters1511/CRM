namespace AppointmentService.Application.Abstractions;

/// <summary>
/// Abstraction for sending an appointment reminder (terminverwaltung.openspec.md, Abschnitt 3,
/// "Terminerinnerung"). KNOWN SIMPLIFICATION: there is no notification/e-mail service in the
/// project yet, so the only implementation registered today is a simple log-based stub
/// (see AppointmentService.Infrastructure.Notifications.LogReminderNotifier). No real SMTP/e-mail
/// dispatch happens. This keeps the reminder Hosted Service (AppointmentReminderHostedService)
/// decoupled from that future implementation detail.
/// </summary>
public interface IReminderNotifier
{
    Task NotifyAsync(AppointmentReminderDto reminder, CancellationToken cancellationToken);
}

public sealed record AppointmentReminderDto(
    Guid AppointmentId,
    Guid CustomerId,
    Guid EmployeeId,
    DateTimeOffset Start,
    DateTimeOffset End);
