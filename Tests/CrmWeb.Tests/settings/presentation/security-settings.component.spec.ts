import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { SecuritySettingsComponent } from '../../../../Clients/crm-web/src/app/settings/presentation/security-settings/security-settings.component';
import { SecuritySettingsState } from '../../../../Clients/crm-web/src/app/settings/application/security-settings.state';

describe('SecuritySettingsComponent', () => {
  it('shows a clean backend-unavailable notice instead of crashing when the identity security API does not exist yet', () => {
    const stateMock = {
      loading: () => false,
      backendUnavailable: () => true,
      hasErrors: () => false,
      errors: () => [],
      saving: () => false,
      policy: () => null,
      devices: () => [],
      auditLog: () => [],
      load: vi.fn(),
      savePolicy: vi.fn(),
      revokeDevice: vi.fn(),
      exportAuditLogAsCsv: vi.fn(() => ''),
    };

    TestBed.configureTestingModule({
      imports: [SecuritySettingsComponent],
      providers: [
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
        { provide: SecuritySettingsState, useValue: stateMock },
      ],
    });

    const fixture = TestBed.createComponent(SecuritySettingsComponent);
    fixture.detectChanges();

    expect(stateMock.load).toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('.alert-warning')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('form')).toBeFalsy();
  });

  it('exports the audit log via the state service when the export button is clicked', () => {
    const stateMock = {
      loading: () => false,
      backendUnavailable: () => false,
      hasErrors: () => false,
      errors: () => [],
      saving: () => false,
      policy: () => ({ mfaRequired: false, minPasswordLength: 8, passwordRequiresSpecialChar: false, sessionTimeoutMinutes: 30 }),
      devices: () => [],
      auditLog: () => [{ id: 'a1', occurredAt: '2026-01-01', actor: 'admin', action: 'login', details: 'ok' }],
      load: vi.fn(),
      savePolicy: vi.fn(),
      revokeDevice: vi.fn(),
      exportAuditLogAsCsv: vi.fn(() => 'Zeitpunkt;Benutzer;Aktion;Details\n2026-01-01;admin;login;ok'),
    };

    TestBed.configureTestingModule({
      imports: [SecuritySettingsComponent],
      providers: [
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
        { provide: SecuritySettingsState, useValue: stateMock },
      ],
    });

    const fixture = TestBed.createComponent(SecuritySettingsComponent);
    fixture.detectChanges();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button.btn-sm');
    button.click();

    expect(stateMock.exportAuditLogAsCsv).toHaveBeenCalled();
  });
});
