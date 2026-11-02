import { Routes } from '@angular/router';
import { SettingsShellComponent } from './presentation/settings-shell/settings-shell.component';
import { GeneralSettingsComponent } from './presentation/general-settings/general-settings.component';
import { TelephonySettingsComponent } from './presentation/telephony-settings/telephony-settings.component';
import { CalendarSyncSettingsComponent } from './presentation/calendar-sync-settings/calendar-sync-settings.component';
import { PaymentProviderSettingsComponent } from './presentation/payment-provider-settings/payment-provider-settings.component';
import { SmtpSettingsComponent } from './presentation/smtp-settings/smtp-settings.component';
import { DatevExportSettingsComponent } from './presentation/datev-export-settings/datev-export-settings.component';
import { UsersRolesSettingsComponent } from './presentation/users-roles-settings/users-roles-settings.component';
import { AiAssistantSettingsComponent } from './presentation/ai-assistant-settings/ai-assistant-settings.component';
import { SecuritySettingsComponent } from './presentation/security-settings/security-settings.component';

/**
 * Lazy-geladene Routen des Einstellungen-Feature-Bereichs (`/settings/**`).
 * `SettingsShellComponent` stellt die Unternavigation bereit, die Unterseiten rendern
 * ueber einen verschachtelten `router-outlet`.
 */
export const SETTINGS_ROUTES: Routes = [
  {
    path: '',
    component: SettingsShellComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'general' },
      { path: 'general', component: GeneralSettingsComponent },
      { path: 'telephony', component: TelephonySettingsComponent },
      { path: 'calendar-sync', component: CalendarSyncSettingsComponent },
      { path: 'payment', component: PaymentProviderSettingsComponent },
      { path: 'smtp', component: SmtpSettingsComponent },
      { path: 'datev', component: DatevExportSettingsComponent },
      { path: 'users-roles', component: UsersRolesSettingsComponent },
      { path: 'ai-assistant', component: AiAssistantSettingsComponent },
      { path: 'security', component: SecuritySettingsComponent },
    ],
  },
];
