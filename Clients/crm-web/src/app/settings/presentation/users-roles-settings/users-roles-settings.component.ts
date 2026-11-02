import { Component, OnInit, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { EmployeeStateService } from '../../../employees/application/employee-state.service';

/**
 * Smart/Container-Component: Benutzer & Rollen. Nutzt die bestehende Mitarbeiter-API
 * (`EmployeeStateService`, echte Daten). Es existiert aktuell KEINE dedizierte
 * Rollenänderungs-API im Backend - die Seite zeigt daher Mitarbeiter/Rollen nur an, statt
 * eine (nicht vorhandene) Aenderung anzubieten (siehe Change-Spec Scope-Abgrenzung).
 */
@Component({
  selector: 'app-users-roles-settings',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './users-roles-settings.component.html',
})
export class UsersRolesSettingsComponent implements OnInit {
  protected readonly state = inject(EmployeeStateService);

  ngOnInit(): void {
    this.state.loadEmployees();
  }
}
