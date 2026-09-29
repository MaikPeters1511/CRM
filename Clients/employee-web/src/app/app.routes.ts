import { Routes } from '@angular/router';
import { EmployeeListComponent } from './presentation/employee-list/employee-list.component';
import { EmployeeFormComponent } from './presentation/employee-form/employee-form.component';
import { EmployeeScheduleComponent } from './presentation/employee-schedule/employee-schedule.component';
import { AbsenceFormComponent } from './presentation/absence-form/absence-form.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'employees' },
  { path: 'employees', component: EmployeeListComponent },
  { path: 'employees/new', component: EmployeeFormComponent },
  { path: 'employees/:id/edit', component: EmployeeFormComponent },
  { path: 'employees/:id/schedule', component: EmployeeScheduleComponent },
  { path: 'employees/:id/absences/new', component: AbsenceFormComponent },
  { path: '**', redirectTo: 'employees' },
];
