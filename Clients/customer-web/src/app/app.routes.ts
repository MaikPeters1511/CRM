import { Routes } from '@angular/router';
import { CustomerListComponent } from './presentation/customer-list/customer-list.component';
import { CustomerFormComponent } from './presentation/customer-form/customer-form.component';
import { CustomerDetailComponent } from './presentation/customer-detail/customer-detail.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'customers' },
  { path: 'customers', component: CustomerListComponent },
  { path: 'customers/new', component: CustomerFormComponent },
  { path: 'customers/:id', component: CustomerDetailComponent },
  { path: 'customers/:id/edit', component: CustomerFormComponent },
  { path: '**', redirectTo: 'customers' },
];
