import { Routes } from '@angular/router';
import { CustomerListComponent } from './presentation/customer-list/customer-list.component';
import { CustomerFormComponent } from './presentation/customer-form/customer-form.component';
import { CustomerDetailComponent } from './presentation/customer-detail/customer-detail.component';

/**
 * Lazy-geladene Routen des Kunden-Feature-Bereichs (`/customers/**`), migriert 1:1 aus
 * `Clients/customer-web/src/app/app.routes.ts` (siehe UserStories/kundenverwaltung.openspec.md).
 */
export const CUSTOMERS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', component: CustomerListComponent },
  { path: 'new', component: CustomerFormComponent },
  { path: ':id', component: CustomerDetailComponent },
  { path: ':id/edit', component: CustomerFormComponent },
];
