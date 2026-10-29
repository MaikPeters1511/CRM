import { Routes } from '@angular/router';
import { ServiceCatalogListComponent } from './presentation/service-catalog-list/service-catalog-list.component';
import { ServiceCatalogFormComponent } from './presentation/service-catalog-form/service-catalog-form.component';

/**
 * Lazy-geladene Routen des Leistungskatalog-Feature-Bereichs (`/catalog/**`), analog
 * `customers.routes.ts`. Nur fuer die Rolle `Admin` relevant (Navigationspunkt in `app.html`
 * ist nur fuer Admin sichtbar); das Backend erzwingt die Admin-Rolle fuer
 * Anlegen/Bearbeiten/Deaktivieren ohnehin serverseitig (siehe
 * UserStories/leistungskatalog.openspec.md Abschnitt 5).
 */
export const CATALOG_ROUTES: Routes = [
  { path: '', pathMatch: 'full', component: ServiceCatalogListComponent },
  { path: 'new', component: ServiceCatalogFormComponent },
  { path: ':id/edit', component: ServiceCatalogFormComponent },
];
