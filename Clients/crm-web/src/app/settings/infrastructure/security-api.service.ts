import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthStateService } from '../../auth/application/auth-state.service';
import { AuditLogEntry, SecurityPolicy, TrustedDevice } from '../domain/settings.model';

interface BackendSecurityPolicyResponse {
  requireMfa?: boolean;
  mfaRequired?: boolean;
  passwordMinLength?: number;
  minPasswordLength?: number;
  passwordExpiryDays?: number;
  maxFailedLoginAttempts?: number;
  lockoutDurationMinutes?: number;
  sessionTimeoutMinutes?: number;
  passwordRequiresSpecialChar?: boolean;
}

interface BackendKnownDeviceResponse {
  id: string;
  userAgent?: string;
  deviceName?: string;
  lastSeenAtUtc?: string;
  lastUsedAt?: string;
  isRevoked?: boolean;
  trusted?: boolean;
}

interface BackendAuditLogEntryResponse {
  id: string;
  occurredAtUtc?: string;
  occurredAt?: string;
  actorUserId?: string;
  actor?: string;
  eventType?: string | number;
  action?: string;
  details: string;
}

const AUDIT_EVENT_TYPES_BY_INDEX: readonly string[] = [
  'LoginSucceeded',
  'LoginFailed',
  'AccountLocked',
  'RoleChanged',
  'PasswordChanged',
  'MfaEnabled',
  'MfaDisabled',
  'PasskeyRegistered',
  'PasskeyRemoved',
  'DeviceConfirmationRequested',
  'DeviceApproved',
  'DeviceRevoked',
  'SecurityPolicyChanged',
  'ApiKeyRotated',
  'CustomerCreated',
  'CustomerUpdated',
  'CustomerDeactivated',
  'EmployeeCreated',
  'EmployeeUpdated',
  'EmployeeDeactivated',
  'WorkingHoursUpdated',
  'AbsenceCreated',
  'AbsenceApproved',
  'AbsenceRejected',
  'AbsenceDeleted',
  'AppointmentCreated',
  'AppointmentRescheduled',
  'AppointmentCancelled',
  'ServiceCreated',
  'ServiceUpdated',
  'ServiceDeactivated',
  'DataChanged',
];

const KNOWN_USERS_BY_ID: Readonly<Record<string, string>> = {
  'ccb1ab35-30ab-4b04-94cb-6d7ee594b49c': 'CRM Admin (admin@crm.local)',
  'e573eed0-8c40-749f-f05c-aa6839c81cb4': 'CRM Employee (employee@crm.local)',
  '00000000-0000-0000-0000-000000000000': 'System / Unbekannt',
};

/**
 * Infrastructure-Service: kapselt die Aufrufe an die Identity-Security-API (MFA-Pflicht,
 * Passwort-Richtlinie, Geraete-Vertrauen, Audit-Log).
 */
@Injectable({ providedIn: 'root' })
export class SecurityApiService {
  private readonly http = inject(HttpClient);
  private readonly authState = inject(AuthStateService);
  private readonly baseUrl = environment.identityApiBaseUrl;

  getPolicy(): Observable<SecurityPolicy> {
    return this.http.get<BackendSecurityPolicyResponse>(`${this.baseUrl}/admin/security-policy`).pipe(
      map((res) => ({
        mfaRequired: res.requireMfa ?? res.mfaRequired ?? false,
        minPasswordLength: res.passwordMinLength ?? res.minPasswordLength ?? 8,
        passwordRequiresSpecialChar: res.passwordRequiresSpecialChar ?? true,
        sessionTimeoutMinutes: res.lockoutDurationMinutes ?? res.sessionTimeoutMinutes ?? 30,
      })),
    );
  }

  updatePolicy(policy: SecurityPolicy): Observable<SecurityPolicy> {
    const payload = {
      ...policy,
      requireMfa: policy.mfaRequired,
      passwordMinLength: policy.minPasswordLength,
      passwordExpiryDays: 90,
      maxFailedLoginAttempts: 5,
      lockoutDurationMinutes: policy.sessionTimeoutMinutes,
    };
    return this.http.put<BackendSecurityPolicyResponse>(`${this.baseUrl}/admin/security-policy`, payload).pipe(
      map((res) => ({
        mfaRequired: res.requireMfa ?? res.mfaRequired ?? policy.mfaRequired,
        minPasswordLength: res.passwordMinLength ?? res.minPasswordLength ?? policy.minPasswordLength,
        passwordRequiresSpecialChar: res.passwordRequiresSpecialChar ?? policy.passwordRequiresSpecialChar,
        sessionTimeoutMinutes: res.lockoutDurationMinutes ?? res.sessionTimeoutMinutes ?? policy.sessionTimeoutMinutes,
      })),
    );
  }

  getTrustedDevices(): Observable<TrustedDevice[]> {
    const userId = this.authState.currentUser()?.userId;
    if (!userId) {
      return of([]);
    }
    return this.http
      .get<BackendKnownDeviceResponse[]>(`${this.baseUrl}/admin/users/${userId}/devices`)
      .pipe(
        map((devices) =>
          devices.map((d) => ({
            id: d.id,
            deviceName: d.deviceName ?? d.userAgent ?? d.id,
            lastUsedAt: d.lastUsedAt ?? d.lastSeenAtUtc ?? '',
            trusted: d.trusted ?? !d.isRevoked,
          })),
        ),
      );
  }

  revokeDevice(deviceId: string): Observable<void> {
    const userId = this.authState.currentUser()?.userId;
    if (!userId) {
      return of(undefined);
    }
    return this.http.post<void>(`${this.baseUrl}/admin/users/${userId}/devices/${deviceId}/revoke`, {});
  }

  getAuditLog(): Observable<AuditLogEntry[]> {
    return this.http
      .get<BackendAuditLogEntryResponse[] | { items: BackendAuditLogEntryResponse[] }>(`${this.baseUrl}/audit-log`)
      .pipe(
        map((res) => {
          const items = Array.isArray(res) ? res : (res.items ?? []);
          return items.map((entry) => ({
            id: entry.id,
            occurredAt: entry.occurredAt ?? entry.occurredAtUtc ?? '',
            actor: this.resolveActorLabel(entry.actor, entry.actorUserId),
            action: this.resolveActionName(entry.action, entry.eventType),
            details: entry.details,
          }));
        }),
      );
  }

  private resolveActorLabel(actor?: string, actorUserId?: string): string {
    const raw = (actor && !this.isUuid(actor) ? actor : actorUserId || actor || '').trim();
    const lower = raw.toLowerCase();
    if (KNOWN_USERS_BY_ID[lower]) {
      return KNOWN_USERS_BY_ID[lower];
    }
    const current = this.authState.currentUser();
    if (current && current.userId.toLowerCase() === lower) {
      return current.displayName;
    }
    return actor || actorUserId || '';
  }

  private resolveActionName(action?: string, eventType?: string | number): string {
    if (action && Number.isNaN(Number(action))) {
      return action;
    }
    if (typeof eventType === 'number') {
      return AUDIT_EVENT_TYPES_BY_INDEX[eventType] ?? String(eventType);
    }
    if (typeof eventType === 'string' && !Number.isNaN(Number(eventType))) {
      return AUDIT_EVENT_TYPES_BY_INDEX[Number(eventType)] ?? eventType;
    }
    return String(action ?? eventType ?? '');
  }

  private isUuid(value: string): boolean {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim());
  }
}
