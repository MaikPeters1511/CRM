namespace IdentityService.Application.Devices;

/// <summary>
/// Abstraktion fuer den Versand des 6-stelligen Geraete-Bestaetigungscodes per E-Mail
/// (identity-device-trust.openspec.md), analog IReminderNotifier aus appointment-service.
/// KNOWN SIMPLIFICATION (docs/adr/0004, Abschnitt 4): die einzige registrierte Implementierung ist
/// aktuell ein Log-Stub (LogDeviceConfirmationNotifier) - kein SMTP-Provider im Projekt konfiguriert.
/// </summary>
public interface IDeviceConfirmationNotifier
{
    Task NotifyAsync(string email, string code, CancellationToken cancellationToken);
}
