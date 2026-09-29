import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { authGuard } from '../../../Clients/appointment-web/src/app/presentation/auth.guard';
import { AuthStateService } from '../../../Clients/appointment-web/src/app/application/auth-state.service';

describe('authGuard', () => {
  function setup(isAuthenticated: boolean) {
    const authStateMock = { isAuthenticated: () => isAuthenticated };
    const routerMock = { createUrlTree: vi.fn().mockReturnValue('url-tree') };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthStateService, useValue: authStateMock },
        { provide: Router, useValue: routerMock },
      ],
    });

    return { routerMock };
  }

  it('allows navigation when the user is authenticated', () => {
    setup(true);

    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as never, { url: '/appointments' } as never),
    );

    expect(result).toBe(true);
  });

  it('redirects to /login with a returnUrl when not authenticated', () => {
    const { routerMock } = setup(false);

    TestBed.runInInjectionContext(() => authGuard({} as never, { url: '/appointments' } as never));

    expect(routerMock.createUrlTree).toHaveBeenCalledWith(
      ['/login'],
      { queryParams: { returnUrl: '/appointments' } },
    );
  });
});
