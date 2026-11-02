using IdentityService.Application.Devices;
using Microsoft.Extensions.Logging;

namespace IdentityService.Infrastructure.Devices;

/// <summary>
/// KNOWN SIMPLIFICATION (identity-device-trust.openspec.md, docs/adr/0004 Abschnitt 4): kein
/// SMTP-Provider im Projekt konfiguriert, daher nur Log-Ausgabe statt echtem E-Mail-Versand.
/// Identisches Muster wie LogReminderNotifier in appointment-service. Echter Versand ist
/// verbindliche Folgeaufgabe (sec-agent/devops-agent).
/// </summary>
public sealed class LogDeviceConfirmationNotifier(ILogger<LogDeviceConfirmationNotifier> logger) : IDeviceConfirmationNotifier
{
    public Task NotifyAsync(string email, string code, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Geraete-Bestaetigungscode (STUB, keine echte E-Mail versendet) fuer {Email}: {Code}",
            email, code);

        return Task.CompletedTask;
    }
}
