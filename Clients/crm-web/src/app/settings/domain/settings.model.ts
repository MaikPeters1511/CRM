/**
 * Domain-Modelle fuer den Einstellungen-Bereich. Die "lokalen" Einstellungstypen
 * (Firmendaten/Telefonie/Kalender-Sync/Zahlungsanbieter/SMTP/DATEV) haben BEWUSST kein
 * Backend-Gegenstueck (siehe Scope-Abgrenzung der Change-Spec) und werden nur im
 * Komponenten-State/`localStorage` gehalten.
 */

export interface OpeningHoursSlot {
  weekday: string;
  from: string;
  to: string;
  closed: boolean;
}

export interface CompanySettings {
  companyName: string;
  logoUrl: string | null;
  street: string;
  zipCode: string;
  city: string;
  phone: string;
  email: string;
  openingHours: OpeningHoursSlot[];
}

export interface TelephonySettings {
  enabled: boolean;
  pbxHost: string;
  extension: string;
  apiKey: string;
}

export interface CalendarSyncSettings {
  provider: 'none' | 'google' | 'microsoft365';
  syncEnabled: boolean;
  calendarId: string;
}

export interface PaymentProviderSettings {
  provider: 'none' | 'stripe';
  publishableKey: string;
  secretKeyConfigured: boolean;
}

export interface SmtpSettings {
  host: string;
  port: number;
  username: string;
  useSsl: boolean;
  senderAddress: string;
}

export interface DatevExportSettings {
  consultantNumber: string;
  clientNumber: string;
  exportFormat: 'csv' | 'xml';
}

/** Entspricht der KI-Einstellungen-Config-API aus `ai-service` (parallele Session). */
export interface AiAssistantSettings {
  enabled: boolean;
  model: string;
  temperature: number;
  suggestionsEnabled: boolean;
}

export type UpdateAiAssistantSettingsRequest = AiAssistantSettings;

/** Entspricht der Identity-Security-API (MFA/Passkeys/Geraete/Audit-Log, parallele Session). */
export interface SecurityPolicy {
  mfaRequired: boolean;
  minPasswordLength: number;
  passwordRequiresSpecialChar: boolean;
  sessionTimeoutMinutes: number;
}

export interface TrustedDevice {
  id: string;
  deviceName: string;
  lastUsedAt: string;
  trusted: boolean;
}

export interface AuditLogEntry {
  id: string;
  occurredAt: string;
  actor: string;
  action: string;
  details: string;
}
