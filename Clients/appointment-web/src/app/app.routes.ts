import { Routes } from '@angular/router';
import { CalendarViewComponent } from './presentation/calendar-view/calendar-view.component';
import { AppointmentFormComponent } from './presentation/appointment-form/appointment-form.component';
import { AppointmentDetailComponent } from './presentation/appointment-detail/appointment-detail.component';
import { LoginComponent } from './presentation/login/login.component';
import { authGuard } from './presentation/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'appointments' },
  { path: 'login', component: LoginComponent },
  { path: 'appointments', component: CalendarViewComponent, canActivate: [authGuard] },
  { path: 'appointments/new', component: AppointmentFormComponent, canActivate: [authGuard] },
  { path: 'appointments/:id', component: AppointmentDetailComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: 'appointments' },
];
