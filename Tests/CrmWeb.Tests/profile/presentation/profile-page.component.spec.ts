import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { ProfilePageComponent } from '../../../../Clients/crm-web/src/app/profile/presentation/profile-page/profile-page.component';
import { AuthStateService } from '../../../../Clients/crm-web/src/app/auth/application/auth-state.service';
import { SecuritySettingsState } from '../../../../Clients/crm-web/src/app/settings/application/security-settings.state';
import { ThemeState } from '../../../../Clients/crm-web/src/app/profile/application/theme.state';

describe('ProfilePageComponent', () => {
  function setup(isAdmin: boolean) {
    const authStateMock = { isAdmin: () => isAdmin, currentUser: () => null };
    const securityStateMock = {
      loading: () => false,
      backendUnavailable: () => true,
      policy: () => null,
      devices: () => [],
      load: vi.fn(),
      revokeDevice: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [ProfilePageComponent],
      providers: [
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
        { provide: AuthStateService, useValue: authStateMock },
        { provide: SecuritySettingsState, useValue: securityStateMock },
      ],
    });

    const fixture = TestBed.createComponent(ProfilePageComponent);
    fixture.detectChanges();
    return { fixture, securityStateMock };
  }

  it('shows the admin theme editor only for admins', () => {
    const { fixture } = setup(true);
    expect(fixture.nativeElement.querySelector('app-admin-theme-editor')).toBeTruthy();
  });

  it('hides the admin theme editor for non-admins', () => {
    const { fixture } = setup(false);
    expect(fixture.nativeElement.querySelector('app-admin-theme-editor')).toBeFalsy();
  });

  it('loads security state on init and switches to the security tab', () => {
    const { fixture, securityStateMock } = setup(false);
    expect(securityStateMock.load).toHaveBeenCalled();

    const tabs = fixture.nativeElement.querySelectorAll('button[role="tab"]');
    (tabs[1] as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.alert-warning')).toBeTruthy();
  });

  it('switches the active DaisyUI theme via ThemeState when a theme button is clicked', () => {
    const { fixture } = setup(false);
    const themeState = TestBed.inject(ThemeState);

    const themeButtons = fixture.nativeElement.querySelectorAll('button[role="radio"]');
    (themeButtons[1] as HTMLButtonElement).click();

    expect(themeState.activeTheme()).not.toBe('');
  });
});
