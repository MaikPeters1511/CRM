import { Routes } from '@angular/router';
import { CalendarViewComponent } from './presentation/calendar-view/calendar-view.component';
import { AppointmentFormComponent } from './presentation/appointment-form/appointment-form.component';
import { AppointmentDetailComponent } from './presentation/appointment-detail/appointment-detail.component';

/**
 * Lazy-geladene Routen des Termin-Feature-Bereichs (`/appointments/**`), migriert 1:1 aus
 * `Clients/appointment-web/src/app/app.routes.ts` (siehe UserStories/terminverwaltung.openspec.md).
 */
export const APPOINTMENTS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', component: CalendarViewComponent },
  { path: 'new', component: AppointmentFormComponent },
  { path: ':id', component: AppointmentDetailComponent },
];
