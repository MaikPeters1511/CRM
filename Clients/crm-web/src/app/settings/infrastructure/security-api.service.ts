import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditLogEntry, SecurityPolicy, TrustedDevice } from '../domain/settings.model';

/**
 * Infrastructure-Service: kapselt die Aufrufe an die Identity-Security-API (MFA-Pflicht,
 * Passwort-Richtlinie, Geraete-Vertrauen, Audit-Log). Entsteht parallel im Branch
 * `identity-mfa-passkeys-audit` (siehe Background dieser Session). Solange der Endpunkt dort
 * noch nicht existiert, liefert der Aufruf 404/503, was die Settings-/Profil-Komponenten als
 * Error-State behandeln statt abzustuerzen.
 */
@Injectable({ providedIn: 'root' })
export class SecurityApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.identityApiBaseUrl}/security`;

  getPolicy(): Observable<SecurityPolicy> {
    return this.http.get<SecurityPolicy>(`${this.baseUrl}/policy`);
  }

  updatePolicy(policy: SecurityPolicy): Observable<SecurityPolicy> {
    return this.http.put<SecurityPolicy>(`${this.baseUrl}/policy`, policy);
  }

  getTrustedDevices(): Observable<TrustedDevice[]> {
    return this.http.get<TrustedDevice[]>(`${this.baseUrl}/devices`);
  }

  revokeDevice(deviceId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/devices/${deviceId}/revoke`, {});
  }

  getAuditLog(): Observable<AuditLogEntry[]> {
    return this.http.get<AuditLogEntry[]>(`${this.baseUrl}/audit-log`);
  }
}
