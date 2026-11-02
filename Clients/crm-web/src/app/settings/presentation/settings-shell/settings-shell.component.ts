import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

interface SettingsNavEntry {
  path: string;
  labelKey: string;
}

/**
 * Smart/Container-Component: Unternavigation des Einstellungen-Bereichs
 * (Allgemein/Telefonie/Kalender-Sync/Zahlungsanbieter/SMTP/DATEV-Export/Benutzer & Rollen/
 * KI-Assistent/Sicherheit). Rein praesentational - die Unterseiten tragen ihre eigene Logik.
 */
@Component({
  selector: 'app-settings-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, TranslatePipe],
  templateUrl: './settings-shell.component.html',
})
export class SettingsShellComponent {
  protected readonly navEntries: SettingsNavEntry[] = [
    { path: 'general', labelKey: 'settings.nav.general' },
    { path: 'telephony', labelKey: 'settings.nav.telephony' },
    { path: 'calendar-sync', labelKey: 'settings.nav.calendarSync' },
    { path: 'payment', labelKey: 'settings.nav.payment' },
    { path: 'smtp', labelKey: 'settings.nav.smtp' },
    { path: 'datev', labelKey: 'settings.nav.datev' },
    { path: 'users-roles', labelKey: 'settings.nav.usersRoles' },
    { path: 'ai-assistant', labelKey: 'settings.nav.aiAssistant' },
    { path: 'security', labelKey: 'settings.nav.security' },
  ];
}
