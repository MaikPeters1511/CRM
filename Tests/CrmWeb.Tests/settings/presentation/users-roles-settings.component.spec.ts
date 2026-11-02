import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { UsersRolesSettingsComponent } from '../../../../Clients/crm-web/src/app/settings/presentation/users-roles-settings/users-roles-settings.component';
import { EmployeeStateService } from '../../../../Clients/crm-web/src/app/employees/application/employee-state.service';

describe('UsersRolesSettingsComponent', () => {
  it('loads employees on init and lists name, email and role (real data, read-only)', () => {
    const stateMock = {
      loading: () => false,
      hasErrors: () => false,
      employees: () => [
        { id: 'e1', firstName: 'Anna', lastName: 'Admin', email: 'anna@example.com', role: 'Admin', isActive: true },
      ],
      loadEmployees: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [UsersRolesSettingsComponent],
      providers: [
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
        { provide: EmployeeStateService, useValue: stateMock },
      ],
    });

    const fixture = TestBed.createComponent(UsersRolesSettingsComponent);
    fixture.detectChanges();

    expect(stateMock.loadEmployees).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Anna Admin');
    expect(fixture.nativeElement.textContent).toContain('anna@example.com');
    // Keine Aktion zum Rollenwechsel vorhanden - es existiert keine Rollenaenderungs-API.
    expect(fixture.nativeElement.querySelector('button')).toBeFalsy();
  });
});
