import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { SecuritySettingsState } from '../../../../Clients/crm-web/src/app/settings/application/security-settings.state';
import { SecurityApiService } from '../../../../Clients/crm-web/src/app/settings/infrastructure/security-api.service';

describe('SecuritySettingsState', () => {
  function setup(apiMock: Partial<SecurityApiService>) {
    TestBed.configureTestingModule({
      providers: [{ provide: SecurityApiService, useValue: apiMock }],
    });
    return TestBed.inject(SecuritySettingsState);
  }

  it('shows a clean backendUnavailable state when the identity security API does not exist yet', () => {
    const state = setup({
      getPolicy: () => throwError(() => new HttpErrorResponse({ status: 404 })),
      getTrustedDevices: () => of([]),
      getAuditLog: () => of([]),
    });

    state.load();

    expect(state.backendUnavailable()).toBe(true);
    expect(state.policy()).toBeNull();
  });

  it('loads policy, devices and audit log on success', () => {
    const state = setup({
      getPolicy: () => of({ mfaRequired: true, minPasswordLength: 12, passwordRequiresSpecialChar: true, sessionTimeoutMinutes: 30 }),
      getTrustedDevices: () => of([{ id: 'd1', deviceName: 'iPhone', lastUsedAt: '2026-01-01', trusted: true }]),
      getAuditLog: () => of([{ id: 'a1', occurredAt: '2026-01-01', actor: 'admin', action: 'login', details: 'ok' }]),
    });

    state.load();

    expect(state.policy()?.mfaRequired).toBe(true);
    expect(state.devices().length).toBe(1);
    expect(state.auditLog().length).toBe(1);
  });

  it('exports the audit log as CSV with a header row', () => {
    const state = setup({
      getPolicy: () => of({ mfaRequired: false, minPasswordLength: 8, passwordRequiresSpecialChar: false, sessionTimeoutMinutes: 60 }),
      getTrustedDevices: () => of([]),
      getAuditLog: () => of([{ id: 'a1', occurredAt: '2026-01-01T10:00:00Z', actor: 'admin', action: 'login', details: 'ok' }]),
    });
    state.load();

    const csv = state.exportAuditLogAsCsv();

    expect(csv.split('\n')[0]).toBe('Zeitpunkt;Benutzer;Aktion;Details');
    expect(csv).toContain('admin;login;ok');
  });

  it('rolls back the optimistic device removal when revoke fails', () => {
    const state = setup({
      getPolicy: () => of({ mfaRequired: false, minPasswordLength: 8, passwordRequiresSpecialChar: false, sessionTimeoutMinutes: 60 }),
      getTrustedDevices: () => of([{ id: 'd1', deviceName: 'iPhone', lastUsedAt: '2026-01-01', trusted: true }]),
      getAuditLog: () => of([]),
      revokeDevice: () => throwError(() => new HttpErrorResponse({ status: 500 })),
    });
    state.load();

    state.revokeDevice('d1');

    expect(state.devices().length).toBe(1);
    expect(state.errors()).toContain('settings.security.errors.revokeFailed');
  });
});
