import { Routes } from '@angular/router';
import { DashboardPageComponent } from './presentation/dashboard-page/dashboard-page.component';

/** Lazy-geladene Route des Dashboard-Feature-Bereichs (`/dashboard`), analog `catalog.routes.ts`. */
export const DASHBOARD_ROUTES: Routes = [{ path: '', pathMatch: 'full', component: DashboardPageComponent }];
