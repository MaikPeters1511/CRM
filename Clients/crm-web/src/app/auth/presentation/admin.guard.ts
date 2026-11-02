import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStateService } from '../application/auth-state.service';

/**
 * Route Guard: beschraenkt eine Route auf die Rolle `Admin` (z.B. Abwesenheits-Freigabe-Ansicht,
 * abwesenheit-workflow.openspec.md Abschnitt 4). Analog zum bestehenden `authGuard`-Muster;
 * laeuft zusaetzlich zu `authGuard` (der die Authentifizierung selbst sicherstellt).
 * Nicht-Admins werden zur Mitarbeiterliste umgeleitet statt einen Fehler zu zeigen.
 */
export const adminGuard: CanActivateFn = () => {
  const authState = inject(AuthStateService);
  const router = inject(Router);

  if (authState.isAdmin()) {
    return true;
  }

  return router.createUrlTree(['/employees']);
};
