import { Routes } from '@angular/router';
import { EmployeeListComponent } from './presentation/employee-list/employee-list.component';
import { EmployeeFormComponent } from './presentation/employee-form/employee-form.component';
import { EmployeeScheduleComponent } from './presentation/employee-schedule/employee-schedule.component';
import { AbsenceFormComponent } from './presentation/absence-form/absence-form.component';
import { LoginComponent } from './presentation/login/login.component';
import { authGuard } from './presentation/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'employees' },
  { path: 'login', component: LoginComponent },
  { path: 'employees', component: EmployeeListComponent, canActivate: [authGuard] },
  { path: 'employees/new', component: EmployeeFormComponent, canActivate: [authGuard] },
  { path: 'employees/:id/edit', component: EmployeeFormComponent, canActivate: [authGuard] },
  { path: 'employees/:id/schedule', component: EmployeeScheduleComponent, canActivate: [authGuard] },
  { path: 'employees/:id/absences/new', component: AbsenceFormComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: 'employees' },
];
