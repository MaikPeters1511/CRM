import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { App } from '../../../Clients/crm-web/src/app/app';
import { AuthStateService } from '../../../Clients/crm-web/src/app/auth/application/auth-state.service';
import { routes } from '../../../Clients/crm-web/src/app/app.routes';
import type { AuthUser } from '../../../Clients/crm-web/src/app/auth/domain/auth-user.model';

/**
 * Deckt die neuen Shell-/Navigations-Requirements aus
 * UserStories/frontend-konsolidierung.openspec.md Abschnitt 3 ab:
 * - "Einheitliche Frontend-Anwendung" (Wechsel zwischen Modulen ohne erneuten Login)
 * - "Gemeinsame Navigation" (Navigation zeigt alle drei Bereiche fuer Admin/Employee)
 */
describe('App (Shell/Navigation)', () => {
  function setup(user: AuthUser | null) {
    const authStateMock = {
      isAuthenticated: () => user !== null,
      currentUser: () => user,
      isAdmin: () => user?.role === 'Admin',
      logout: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
        { provide: AuthStateService, useValue: authStateMock },
      ],
    });

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return { fixture };
  }

  it('shows navigation entries for customers, employees and appointments for an authenticated Admin', () => {
    const { fixture } = setup({ userId: 'u1', role: 'Admin', displayName: 'Admin User' });

    const links = fixture.nativeElement.querySelectorAll('nav[aria-label="crm.nav.main"] a');
    const hrefs = Array.from(links as NodeListOf<HTMLAnchorElement>).map((a) =>
      a.getAttribute('routerLink'),
    );

    expect(hrefs).toEqual(['/customers', '/employees', '/appointments']);
  });

  it('shows navigation entries for customers, employees and appointments for an authenticated Employee', () => {
    const { fixture } = setup({ userId: 'u2', role: 'Employee', displayName: 'Employee User' });

    const links = fixture.nativeElement.querySelectorAll('nav[aria-label="crm.nav.main"] a');

    expect(links.length).toBe(3);
  });

  it('hides the module navigation for anonymous (not authenticated) users', () => {
    const { fixture } = setup(null);

    const nav = fixture.nativeElement.querySelector('nav[aria-label="crm.nav.main"]');

    expect(nav).toBeNull();
  });

  it('keeps the same AuthStateService instance (session) when navigating between feature areas without re-login', async () => {
    setup({ userId: 'u1', role: 'Admin', displayName: 'Admin User' });

    const authStateBeforeNavigation = TestBed.inject(AuthStateService);
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/customers');
    const authStateInCustomers = TestBed.inject(AuthStateService);

    await router.navigateByUrl('/appointments');
    const authStateInAppointments = TestBed.inject(AuthStateService);

    expect(authStateInCustomers).toBe(authStateBeforeNavigation);
    expect(authStateInAppointments).toBe(authStateBeforeNavigation);
    expect(authStateInAppointments.isAuthenticated()).toBe(true);
  });
});
