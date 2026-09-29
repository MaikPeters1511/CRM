import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuthStateService } from '../../../Clients/customer-web/src/app/application/auth-state.service';
import { AuthApiService } from '../../../Clients/customer-web/src/app/infrastructure/auth-api.service';
import { AuthTokenStorage } from '../../../Clients/customer-web/src/app/infrastructure/auth-token.storage';

/**
 * Deckt die Security-Requirements (Login, Session-Speicherung, invalide Anmeldedaten)
 * aus UserStories/authentifizierung.openspec.md Abschnitt 3 ab.
 */
describe('AuthStateService', () => {
  const loginResponse = {
    accessToken: 'fake-jwt-token',
    expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
    userId: 'user-1',
    role: 'Admin' as const,
    displayName: 'CRM Admin',
  };

  function setup() {
    const apiMock = { login: vi.fn().mockReturnValue(of(loginResponse)) };

    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        AuthStateService,
        AuthTokenStorage,
        { provide: AuthApiService, useValue: apiMock },
      ],
    });

    return { service: TestBed.inject(AuthStateService), apiMock };
  }

  it('stores the token/user and marks the user as authenticated on successful login', () => {
    const { service } = setup();
    const onSuccess = vi.fn();

    service.login({ email: 'admin@crm.local', password: 'correct' }, onSuccess);

    expect(onSuccess).toHaveBeenCalled();
    expect(service.isAuthenticated()).toBe(true);
    expect(service.currentUser()?.role).toBe('Admin');
    expect(sessionStorage.getItem('crm.auth.token')).toBe('fake-jwt-token');
  });

  it('sets an error and does not authenticate on invalid credentials (401)', () => {
    const apiMock = { login: vi.fn().mockReturnValue(throwError(() => new Error('401'))) };
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        AuthStateService,
        AuthTokenStorage,
        { provide: AuthApiService, useValue: apiMock },
      ],
    });
    const service = TestBed.inject(AuthStateService);
    const onSuccess = vi.fn();

    service.login({ email: 'admin@crm.local', password: 'wrong' }, onSuccess);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.error()).toBe('auth.login.errors.invalidCredentials');
  });

  it('clears the session on logout', () => {
    const { service } = setup();
    service.login({ email: 'admin@crm.local', password: 'correct' }, () => {});
    expect(service.isAuthenticated()).toBe(true);

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem('crm.auth.token')).toBeNull();
  });
});
