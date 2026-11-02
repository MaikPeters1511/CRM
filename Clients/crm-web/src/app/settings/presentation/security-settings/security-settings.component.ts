import { Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LocalDateTimePipe, LocaleFormatService } from '../../../shared/formatting/locale-format.service';
import { SecuritySettingsState } from '../../application/security-settings.state';

const AUDIT_ACTION_LABELS_DE: Readonly<Record<string, string>> = {
  LoginSucceeded: 'Anmeldung erfolgreich',
  LoginFailed: 'Anmeldung fehlgeschlagen',
  AccountLocked: 'Konto gesperrt',
  RoleChanged: 'Rolle geändert',
  PasswordChanged: 'Passwort geändert',
  MfaEnabled: 'MFA aktiviert',
  MfaDisabled: 'MFA deaktiviert',
  PasskeyRegistered: 'Passkey registriert',
  PasskeyRemoved: 'Passkey entfernt',
  DeviceConfirmationRequested: 'Gerätebestätigung angefordert',
  DeviceApproved: 'Gerät bestätigt',
  DeviceRevoked: 'Gerät widerrufen',
  SecurityPolicyChanged: 'Sicherheitsrichtlinie geändert',
  ApiKeyRotated: 'API-Schlüssel rotiert',
  CustomerCreated: 'Kunde erstellt',
  CustomerUpdated: 'Kunde geändert',
  CustomerDeactivated: 'Kunde deaktiviert',
  EmployeeCreated: 'Mitarbeiter erstellt',
  EmployeeUpdated: 'Mitarbeiter geändert',
  EmployeeDeactivated: 'Mitarbeiter deaktiviert',
  WorkingHoursUpdated: 'Arbeitszeiten geändert',
  AbsenceCreated: 'Abwesenheit erstellt',
  AbsenceApproved: 'Abwesenheit genehmigt',
  AbsenceRejected: 'Abwesenheit abgelehnt',
  AbsenceDeleted: 'Abwesenheit gelöscht',
  AppointmentCreated: 'Termin erstellt',
  AppointmentRescheduled: 'Termin verschoben',
  AppointmentCancelled: 'Termin storniert',
  ServiceCreated: 'Leistung erstellt',
  ServiceUpdated: 'Leistung geändert',
  ServiceDeactivated: 'Leistung deaktiviert',
  DataChanged: 'Daten geändert',
};

const AUDIT_ACTION_LABELS_EN: Readonly<Record<string, string>> = {
  LoginSucceeded: 'Login succeeded',
  LoginFailed: 'Login failed',
  AccountLocked: 'Account locked',
  RoleChanged: 'Role changed',
  PasswordChanged: 'Password changed',
  MfaEnabled: 'MFA enabled',
  MfaDisabled: 'MFA disabled',
  PasskeyRegistered: 'Passkey registered',
  PasskeyRemoved: 'Passkey removed',
  DeviceConfirmationRequested: 'Device confirmation requested',
  DeviceApproved: 'Device approved',
  DeviceRevoked: 'Device revoked',
  SecurityPolicyChanged: 'Security policy changed',
  ApiKeyRotated: 'API key rotated',
  CustomerCreated: 'Customer created',
  CustomerUpdated: 'Customer updated',
  CustomerDeactivated: 'Customer deactivated',
  EmployeeCreated: 'Employee created',
  EmployeeUpdated: 'Employee updated',
  EmployeeDeactivated: 'Employee deactivated',
  WorkingHoursUpdated: 'Working hours updated',
  AbsenceCreated: 'Absence created',
  AbsenceApproved: 'Absence approved',
  AbsenceRejected: 'Absence rejected',
  AbsenceDeleted: 'Absence deleted',
  AppointmentCreated: 'Appointment created',
  AppointmentRescheduled: 'Appointment rescheduled',
  AppointmentCancelled: 'Appointment cancelled',
  ServiceCreated: 'Service created',
  ServiceUpdated: 'Service updated',
  ServiceDeactivated: 'Service deactivated',
  DataChanged: 'Data changed',
};

/**
 * Smart/Container-Component: Sicherheits-Einstellungen (Passwort-Richtlinie, MFA-Pflicht,
 * vertrauenswuerdige Geraete, Audit-Log mit CSV-Export) gegen die Identity-Security-API, die
 * parallel im Branch `identity-mfa-passkeys-audit` entsteht. Zeigt einen Error-State, solange
 * der Endpunkt dort noch nicht existiert.
 */
@Component({
  selector: 'app-security-settings',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe, LocalDateTimePipe],
  templateUrl: './security-settings.component.html',
})
export class SecuritySettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  protected readonly localeFormat = inject(LocaleFormatService);
  protected readonly state = inject(SecuritySettingsState);

  protected readonly form = this.fb.nonNullable.group({
    mfaRequired: [false],
    minPasswordLength: [8],
    passwordRequiresSpecialChar: [false],
    sessionTimeoutMinutes: [30],
  });

  constructor() {
    effect(() => {
      const policy = this.state.policy();
      if (policy) {
        this.form.setValue(policy);
      }
    });
  }

  ngOnInit(): void {
    this.state.load();
  }

  savePolicy(): void {
    this.state.savePolicy(this.form.getRawValue(), () => {});
  }

  revokeDevice(deviceId: string): void {
    this.state.revokeDevice(deviceId);
  }

  exportAuditLog(): void {
    const csv = this.state.exportAuditLogAsCsv();
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'audit-log.csv';
    link.click();
    URL.revokeObjectURL(url);
  }

  formatAction(action: string): string {
    const dict = this.localeFormat.lang() === 'en' ? AUDIT_ACTION_LABELS_EN : AUDIT_ACTION_LABELS_DE;
    return dict[action] ?? action;
  }

  actionBadgeClass(action: string): string {
    if (
      action.endsWith('Created') ||
      action.endsWith('Registered') ||
      action.endsWith('Approved') ||
      action === 'LoginSucceeded' ||
      action === 'MfaEnabled'
    ) {
      return 'badge-success badge-soft';
    }
    if (action.endsWith('Updated') || action.endsWith('Changed') || action === 'AppointmentRescheduled' || action === 'ApiKeyRotated') {
      return 'badge-info badge-soft';
    }
    if (
      action.endsWith('Deleted') ||
      action.endsWith('Deactivated') ||
      action.endsWith('Cancelled') ||
      action.endsWith('Revoked') ||
      action.endsWith('Rejected') ||
      action === 'LoginFailed' ||
      action === 'AccountLocked' ||
      action === 'MfaDisabled'
    ) {
      return 'badge-error badge-soft';
    }
    if (action === 'DeviceConfirmationRequested') {
      return 'badge-warning badge-soft';
    }
    return 'badge-ghost badge-soft';
  }
}
