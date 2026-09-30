import { Routes } from '@angular/router';
import { EmployeeListComponent } from './presentation/employee-list/employee-list.component';
import { EmployeeFormComponent } from './presentation/employee-form/employee-form.component';
import { EmployeeScheduleComponent } from './presentation/employee-schedule/employee-schedule.component';
import { AbsenceFormComponent } from './presentation/absence-form/absence-form.component';

/**
 * Lazy-geladene Routen des Mitarbeiter-Feature-Bereichs (`/employees/**`), migriert 1:1 aus
 * `Clients/employee-web/src/app/app.routes.ts` (siehe UserStories/mitarbeiterverwaltung.openspec.md).
 */
export const EMPLOYEES_ROUTES: Routes = [
  { path: '', pathMatch: 'full', component: EmployeeListComponent },
  { path: 'new', component: EmployeeFormComponent },
  { path: ':id/edit', component: EmployeeFormComponent },
  { path: ':id/schedule', component: EmployeeScheduleComponent },
  { path: ':id/absences/new', component: AbsenceFormComponent },
];
