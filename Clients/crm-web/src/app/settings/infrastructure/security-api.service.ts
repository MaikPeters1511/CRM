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
            actor: entry.actor ?? entry.actorUserId ?? '',
            action: String(entry.action ?? entry.eventType ?? ''),
            details: entry.details,
          }));
        }),
      );
  }
}
