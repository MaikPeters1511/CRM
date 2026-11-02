import { TestBed } from '@angular/core/testing';
import { LocalSettingsState } from '../../../../Clients/crm-web/src/app/settings/application/local-settings.state';
import { installLocalStorageMock } from '../../support/local-storage.mock';

describe('LocalSettingsState', () => {
  beforeEach(() => {
    installLocalStorageMock();
    TestBed.configureTestingModule({});
  });

  it('starts with sensible defaults when nothing was persisted yet', () => {
    const state = TestBed.inject(LocalSettingsState);

    expect(state.company().companyName).toBe('');
    expect(state.telephony().enabled).toBe(false);
    expect(state.smtp().port).toBe(587);
  });

  it('persists company settings to localStorage and restores them on the next load', () => {
    const state = TestBed.inject(LocalSettingsState);

    state.saveCompany({
      companyName: 'MP CRM GmbH',
      logoUrl: null,
      street: 'Hauptstr. 1',
      zipCode: '12345',
      city: 'Berlin',
      phone: '+49 30 1234567',
      email: 'kontakt@mp-crm.example',
      openingHours: [],
    });

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const reloaded = TestBed.inject(LocalSettingsState);

    expect(reloaded.company().companyName).toBe('MP CRM GmbH');
    expect(reloaded.company().city).toBe('Berlin');
  });

  it('persists each local settings block independently', () => {
    const state = TestBed.inject(LocalSettingsState);

    state.saveSmtp({ host: 'smtp.example.com', port: 465, username: 'bot', useSsl: true, senderAddress: 'bot@example.com' });
    state.saveDatev({ consultantNumber: '1001', clientNumber: '2002', exportFormat: 'xml' });

    expect(state.smtp().host).toBe('smtp.example.com');
    expect(state.datev().exportFormat).toBe('xml');
    expect(state.telephony().enabled).toBe(false);
  });
});
