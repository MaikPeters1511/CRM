import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { adminGuard } from '../../../../Clients/crm-web/src/app/auth/presentation/admin.guard';
import { AuthStateService } from '../../../../Clients/crm-web/src/app/auth/application/auth-state.service';

/**
 * Deckt das RBAC-Requirement "nur Admin darf die Abwesenheits-Freigabe-Ansicht erreichen" ab
 * (abwesenheit-workflow.openspec.md Abschnitt 4), analog zum bestehenden `authGuard`-Muster.
 */
describe('adminGuard', () => {
  function setup(isAdmin: boolean) {
    const authStateMock = { isAdmin: () => isAdmin };
    const routerMock = { createUrlTree: vi.fn().mockReturnValue('url-tree') };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthStateService, useValue: authStateMock },
        { provide: Router, useValue: routerMock },
      ],
    });

    return { routerMock };
  }

  it('allows navigation for Admin', () => {
    setup(true);

    const result = TestBed.runInInjectionContext(() => adminGuard({} as never, {} as never));

    expect(result).toBe(true);
  });

  it('redirects non-Admin users away (Scenario: Mitarbeiter versucht eine Anfrage zu genehmigen)', () => {
    const { routerMock } = setup(false);

    TestBed.runInInjectionContext(() => adminGuard({} as never, {} as never));

    expect(routerMock.createUrlTree).toHaveBeenCalledWith(['/employees']);
  });
});
