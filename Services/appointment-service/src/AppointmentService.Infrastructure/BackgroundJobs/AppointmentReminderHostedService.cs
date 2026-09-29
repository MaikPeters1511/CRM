using AppointmentService.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppointmentService.Infrastructure.BackgroundJobs;

/// <summary>
/// Hosted Service implementing "Terminerinnerung" (terminverwaltung.openspec.md, Abschnitt 3/7):
/// periodically looks for confirmed appointments starting in ~24 hours and notifies customer and
/// employee via <see cref="IReminderNotifier"/>. KNOWN SIMPLIFICATION: this is a simple polling
/// Hosted Service (no Cron/Quartz/Aspire worker project), matching the current project scope - no
/// real e-mail dispatch happens (see <see cref="Notifications.LogReminderNotifier"/>).
/// Idempotent via <c>Appointment.ReminderSentAt</c>: an appointment is only ever notified once.
/// </summary>
public sealed class AppointmentReminderHostedService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<AppointmentReminderHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReminderLeadTime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ReminderWindowTolerance = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to process due appointment reminders.");
            }

            try
            {
                await Task.Delay(PollInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested - exit the loop.
            }
        }
    }

    private async Task SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();
        var notifier = scope.ServiceProvider.GetRequiredService<IReminderNotifier>();

        var now = timeProvider.GetUtcNow();
        var windowStart = now + ReminderLeadTime - ReminderWindowTolerance;
        var windowEnd = now + ReminderLeadTime + ReminderWindowTolerance;

        var dueAppointments = await repository.GetUpcomingConfirmedWithoutReminderAsync(
            windowStart, windowEnd, cancellationToken);

        foreach (var appointment in dueAppointments)
        {
            await notifier.NotifyAsync(
                new AppointmentReminderDto(
                    appointment.Id, appointment.CustomerId, appointment.EmployeeId, appointment.Start, appointment.End),
                cancellationToken);

            appointment.MarkReminderSent(now);
        }

        if (dueAppointments.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
    }
}
