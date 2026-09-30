import { Routes } from '@angular/router';
import { LoginComponent } from './auth/presentation/login/login.component';
import { authGuard } from './auth/presentation/auth.guard';

/**
 * Gemeinsame Shell-Routing-Konfiguration (siehe `docs/adr/0002-frontend-konsolidierung.md`):
 * ein Login-Flow, drei lazy-geladene Feature-Bereiche `customers`, `employees`, `appointments`.
 * Wechsel zwischen den Bereichen erfordert keinen erneuten Login (gemeinsamer `authGuard`/
 * `AuthStateService`), siehe Requirement "Einheitliche Frontend-Anwendung" in
 * UserStories/frontend-konsolidierung.openspec.md Abschnitt 3.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'customers' },
  { path: 'login', component: LoginComponent },
  {
    path: 'customers',
    canActivate: [authGuard],
    loadChildren: () => import('./customers/customers.routes').then((m) => m.CUSTOMERS_ROUTES),
  },
  {
    path: 'employees',
    canActivate: [authGuard],
    loadChildren: () => import('./employees/employees.routes').then((m) => m.EMPLOYEES_ROUTES),
  },
  {
    path: 'appointments',
    canActivate: [authGuard],
    loadChildren: () =>
      import('./appointments/appointments.routes').then((m) => m.APPOINTMENTS_ROUTES),
  },
  { path: '**', redirectTo: 'customers' },
];
