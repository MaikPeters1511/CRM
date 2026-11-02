import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { SettingsShellComponent } from '../../../../Clients/crm-web/src/app/settings/presentation/settings-shell/settings-shell.component';
import { SETTINGS_ROUTES } from '../../../../Clients/crm-web/src/app/settings/settings.routes';

describe('SettingsShellComponent', () => {
  it('renders a navigation link for every settings sub-area', () => {
    TestBed.configureTestingModule({
      imports: [SettingsShellComponent],
      providers: [provideRouter(SETTINGS_ROUTES), provideTranslateService({ lang: 'de', fallbackLang: 'de' })],
    });

    const fixture = TestBed.createComponent(SettingsShellComponent);
    fixture.detectChanges();

    const links = fixture.nativeElement.querySelectorAll('nav a');

    expect(links.length).toBe(9);
  });
});
