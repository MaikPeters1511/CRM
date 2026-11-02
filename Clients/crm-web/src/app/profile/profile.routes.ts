import { Routes } from '@angular/router';
import { ProfilePageComponent } from './presentation/profile-page/profile-page.component';

/** Lazy-geladene Route des Profil-Bereichs (`/profile`). */
export const PROFILE_ROUTES: Routes = [{ path: '', component: ProfilePageComponent }];
