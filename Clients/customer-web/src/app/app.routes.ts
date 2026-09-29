import { Routes } from '@angular/router';
import { CustomerListComponent } from './presentation/customer-list/customer-list.component';
import { CustomerFormComponent } from './presentation/customer-form/customer-form.component';
import { CustomerDetailComponent } from './presentation/customer-detail/customer-detail.component';
import { LoginComponent } from './presentation/login/login.component';
import { authGuard } from './presentation/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'customers' },
  { path: 'login', component: LoginComponent },
  { path: 'customers', component: CustomerListComponent, canActivate: [authGuard] },
  { path: 'customers/new', component: CustomerFormComponent, canActivate: [authGuard] },
  { path: 'customers/:id', component: CustomerDetailComponent, canActivate: [authGuard] },
  { path: 'customers/:id/edit', component: CustomerFormComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: 'customers' },
];
