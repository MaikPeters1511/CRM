import { Injectable, signal } from '@angular/core';
import {
  CalendarSyncSettings,
  CompanySettings,
  DatevExportSettings,
  PaymentProviderSettings,
  SmtpSettings,
  TelephonySettings,
} from '../domain/settings.model';

const STORAGE_KEY = 'crm.settings.local';

const DEFAULT_COMPANY: CompanySettings = {
  companyName: '',
  logoUrl: null,
  street: '',
  zipCode: '',
  city: '',
  phone: '',
  email: '',
  openingHours: [
    { weekday: 'Monday', from: '09:00', to: '18:00', closed: false },
    { weekday: 'Tuesday', from: '09:00', to: '18:00', closed: false },
    { weekday: 'Wednesday', from: '09:00', to: '18:00', closed: false },
    { weekday: 'Thursday', from: '09:00', to: '18:00', closed: false },
    { weekday: 'Friday', from: '09:00', to: '18:00', closed: false },
    { weekday: 'Saturday', from: '09:00', to: '13:00', closed: false },
    { weekday: 'Sunday', from: '00:00', to: '00:00', closed: true },
  ],
};

const DEFAULT_TELEPHONY: TelephonySettings = {
  enabled: false,
  pbxHost: '',
  extension: '',
  apiKey: '',
};

const DEFAULT_CALENDAR_SYNC: CalendarSyncSettings = {
  provider: 'none',
  syncEnabled: false,
  calendarId: '',
};

const DEFAULT_PAYMENT: PaymentProviderSettings = {
  provider: 'none',
  publishableKey: '',
  secretKeyConfigured: false,
};

const DEFAULT_SMTP: SmtpSettings = {
  host: '',
  port: 587,
  username: '',
  useSsl: true,
  senderAddress: '',
};

const DEFAULT_DATEV: DatevExportSettings = {
  consultantNumber: '',
  clientNumber: '',
  exportFormat: 'csv',
};

interface LocalSettingsSnapshot {
  company: CompanySettings;
  telephony: TelephonySettings;
  calendarSync: CalendarSyncSettings;
  payment: PaymentProviderSettings;
  smtp: SmtpSettings;
  datev: DatevExportSettings;
}

/**
 * Application-Layer: Einstellungen ohne Backend-Anbindung (Firmendaten/Telefonie/
 * Kalender-Sync/Zahlungsanbieter/SMTP/DATEV-Export - siehe Scope-Abgrenzung der
 * Change-Spec: fuer diese vier/fuenf Integrationsbereiche existiert aktuell KEIN Backend
 * und wird es in dieser Aufgabe auch nicht geben). Persistiert rein im `localStorage` dieses
 * Browsers, kein Anspruch auf Server-Synchronisation oder Mehrbenutzer-Konsistenz.
 */
@Injectable({ providedIn: 'root' })
export class LocalSettingsState {
  private readonly snapshot = this.loadSnapshot();

  private readonly _company = signal<CompanySettings>(this.snapshot.company);
  private readonly _telephony = signal<TelephonySettings>(this.snapshot.telephony);
  private readonly _calendarSync = signal<CalendarSyncSettings>(this.snapshot.calendarSync);
  private readonly _payment = signal<PaymentProviderSettings>(this.snapshot.payment);
  private readonly _smtp = signal<SmtpSettings>(this.snapshot.smtp);
  private readonly _datev = signal<DatevExportSettings>(this.snapshot.datev);

  readonly company = this._company.asReadonly();
  readonly telephony = this._telephony.asReadonly();
  readonly calendarSync = this._calendarSync.asReadonly();
  readonly payment = this._payment.asReadonly();
  readonly smtp = this._smtp.asReadonly();
  readonly datev = this._datev.asReadonly();

  saveCompany(value: CompanySettings): void {
    this._company.set(value);
    this.persist();
  }

  saveTelephony(value: TelephonySettings): void {
    this._telephony.set(value);
    this.persist();
  }

  saveCalendarSync(value: CalendarSyncSettings): void {
    this._calendarSync.set(value);
    this.persist();
  }

  savePayment(value: PaymentProviderSettings): void {
    this._payment.set(value);
    this.persist();
  }

  saveSmtp(value: SmtpSettings): void {
    this._smtp.set(value);
    this.persist();
  }

  saveDatev(value: DatevExportSettings): void {
    this._datev.set(value);
    this.persist();
  }

  private loadSnapshot(): LocalSettingsSnapshot {
    const defaults: LocalSettingsSnapshot = {
      company: DEFAULT_COMPANY,
      telephony: DEFAULT_TELEPHONY,
      calendarSync: DEFAULT_CALENDAR_SYNC,
      payment: DEFAULT_PAYMENT,
      smtp: DEFAULT_SMTP,
      datev: DEFAULT_DATEV,
    };
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return defaults;
      }
      const parsed = JSON.parse(raw) as Partial<LocalSettingsSnapshot>;
      return { ...defaults, ...parsed };
    } catch {
      return defaults;
    }
  }

  private persist(): void {
    const snapshot: LocalSettingsSnapshot = {
      company: this._company(),
      telephony: this._telephony(),
      calendarSync: this._calendarSync(),
      payment: this._payment(),
      smtp: this._smtp(),
      datev: this._datev(),
    };
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot));
    } catch {
      // localStorage kann in seltenen Faellen nicht verfuegbar sein (z.B. Privatmodus mit
      // Speicherlimit) - Einstellungen bleiben dann nur fuer die aktuelle Sitzung erhalten.
    }
  }
}
