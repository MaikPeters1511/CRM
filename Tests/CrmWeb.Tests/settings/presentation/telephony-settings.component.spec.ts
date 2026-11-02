import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { TelephonySettingsComponent } from '../../../../Clients/crm-web/src/app/settings/presentation/telephony-settings/telephony-settings.component';
import { LocalSettingsState } from '../../../../Clients/crm-web/src/app/settings/application/local-settings.state';
import { installLocalStorageMock } from '../../support/local-storage.mock';

/**
 * Deckt den gemeinsamen Pattern aller fuenf lokalen Integrations-Settings-Seiten
 * (Telefonie/Kalender-Sync/Zahlungsanbieter/SMTP/DATEV-Export) ab: Formular -> Speichern ->
 * `LocalSettingsState` -> "Gespeichert"-Hinweis. Die anderen vier Komponenten folgen
 * strukturell demselben, hier getesteten Muster.
 */
describe('TelephonySettingsComponent', () => {
  beforeEach(() => installLocalStorageMock());

  it('saves the form value into LocalSettingsState and shows a saved indicator', () => {
    TestBed.configureTestingModule({
      imports: [TelephonySettingsComponent],
      providers: [provideTranslateService({ lang: 'de', fallbackLang: 'de' })],
    });

    const fixture = TestBed.createComponent(TelephonySettingsComponent);
    fixture.detectChanges();
    const state = TestBed.inject(LocalSettingsState);

    fixture.componentInstance['form'].setValue({
      enabled: true,
      pbxHost: 'pbx.example.com',
      extension: '101',
      apiKey: 'secret',
    });
    fixture.componentInstance.save();
    fixture.detectChanges();

    expect(state.telephony().pbxHost).toBe('pbx.example.com');
    expect(fixture.componentInstance['savedVisible']()).toBe(true);
    expect(fixture.nativeElement.querySelector('[role="status"].text-success')).toBeTruthy();
  });
});
