import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, finalize, forkJoin, of, tap } from 'rxjs';
import { AuditLogEntry, SecurityPolicy, TrustedDevice } from '../domain/settings.model';
import { SecurityApiService } from '../infrastructure/security-api.service';
import { isBackendUnavailable } from '../infrastructure/api-error.util';

/**
 * Application-Layer: State fuer den Sicherheits-Einstellungsbereich (Passwort-Richtlinie,
 * MFA-Pflicht, vertrauenswuerdige Geraete, Audit-Log) gegen die Identity-Security-API, die
 * parallel im Branch `identity-mfa-passkeys-audit` entsteht. Solange der Endpunkt noch nicht
 * existiert, zeigt `backendUnavailable` einen Error-State statt eines Crashes.
 */
@Injectable({ providedIn: 'root' })
export class SecuritySettingsState {
  private readonly api = inject(SecurityApiService);

  private readonly _policy = signal<SecurityPolicy | null>(null);
  private readonly _devices = signal<TrustedDevice[]>([]);
  private readonly _auditLog = signal<AuditLogEntry[]>([]);
  private readonly _loading = signal(false);
  private readonly _saving = signal(false);
  private readonly _backendUnavailable = signal(false);
  private readonly _errors = signal<string[]>([]);

  readonly policy = this._policy.asReadonly();
  readonly devices = this._devices.asReadonly();
  readonly auditLog = this._auditLog.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly saving = this._saving.asReadonly();
  readonly backendUnavailable = this._backendUnavailable.asReadonly();
  readonly errors = this._errors.asReadonly();
  readonly hasErrors = computed(() => this._errors().length > 0);

  load(): void {
    this._loading.set(true);
    this._errors.set([]);
    this._backendUnavailable.set(false);
    forkJoin({
      policy: this.api.getPolicy(),
      devices: this.api.getTrustedDevices(),
      auditLog: this.api.getAuditLog(),
    })
      .pipe(
        tap((result) => {
          this._policy.set(result.policy);
          this._devices.set(result.devices);
          this._auditLog.set(result.auditLog);
        }),
        catchError((error: unknown) => {
          if (isBackendUnavailable(error)) {
            this._backendUnavailable.set(true);
          } else {
            this._errors.set(['settings.security.errors.loadFailed']);
          }
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  savePolicy(policy: SecurityPolicy, onSuccess: () => void): void {
    this._saving.set(true);
    this._errors.set([]);
    const previous = this._policy();

    this.api
      .updatePolicy(policy)
      .pipe(
        tap((saved) => {
          this._policy.set(saved);
          onSuccess();
        }),
        catchError(() => {
          this._policy.set(previous);
          this._errors.set(['settings.security.errors.saveFailed']);
          return of(null);
        }),
        finalize(() => this._saving.set(false)),
      )
      .subscribe();
  }

  revokeDevice(deviceId: string): void {
    const previous = this._devices();
    this._devices.update((list) => list.filter((d) => d.id !== deviceId));

    this.api
      .revokeDevice(deviceId)
      .pipe(
        catchError(() => {
          this._devices.set(previous);
          this._errors.set(['settings.security.errors.revokeFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }

  exportAuditLogAsCsv(): string {
    const header = 'Zeitpunkt;Benutzer;Aktion;Details';
    const rows = this._auditLog().map(
      (entry) => `${entry.occurredAt};${entry.actor};${entry.action};${entry.details}`,
    );
    return [header, ...rows].join('\n');
  }
}
