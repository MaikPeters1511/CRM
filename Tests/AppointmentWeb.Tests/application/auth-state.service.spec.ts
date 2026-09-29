import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuthStateService } from '../../../Clients/appointment-web/src/app/application/auth-state.service';
import { AuthApiService } from '../../../Clients/appointment-web/src/app/infrastructure/auth-api.service';
import { AuthTokenStorage } from '../../../Clients/appointment-web/src/app/infrastructure/auth-token.storage';

/**
 * Deckt den Login-Flow gegen identity-service ab (analog customer-web/employee-web,
 * terminverwaltung.openspec.md Abschnitt 5 "Authentifizierung/Autorisierung").
 */
describe('AuthStateService', () => {
  const loginResponse = {
    accessToken: 'fake-jwt-token',
    expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
    userId: 'user-1',
    role: 'Employee' as const,
    displayName: 'CRM Mitarbeiter',
  };

  function setup(apiMock = { login: vi.fn().mockReturnValue(of(loginResponse)) }) {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [AuthStateService, AuthTokenStorage, { provide: AuthApiService, useValue: apiMock }],
    });
    return { service: TestBed.inject(AuthStateService), apiMock };
  }

  it('stores the token/user and marks the user as authenticated on successful login', () => {
    const { service } = setup();
    const onSuccess = vi.fn();

    service.login({ email: 'employee@crm.local', password: 'correct' }, onSuccess);

    expect(onSuccess).toHaveBeenCalled();
    expect(service.isAuthenticated()).toBe(true);
    expect(service.currentUser()?.role).toBe('Employee');
    expect(sessionStorage.getItem('crm.auth.token')).toBe('fake-jwt-token');
  });

  it('sets an error and does not authenticate on invalid credentials', () => {
    const { service } = setup({ login: vi.fn().mockReturnValue(throwError(() => new Error('401'))) });
    const onSuccess = vi.fn();

    service.login({ email: 'employee@crm.local', password: 'wrong' }, onSuccess);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.error()).toBe('auth.login.errors.invalidCredentials');
  });

  it('clears the session on logout', () => {
    const { service } = setup();
    service.login({ email: 'employee@crm.local', password: 'correct' }, () => {});
    expect(service.isAuthenticated()).toBe(true);

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem('crm.auth.token')).toBeNull();
  });
});
