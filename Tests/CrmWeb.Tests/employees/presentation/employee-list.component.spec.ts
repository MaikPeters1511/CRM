import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { EmployeeListComponent } from '../../../../Clients/crm-web/src/app/employees/presentation/employee-list/employee-list.component';
import { EmployeeStateService } from '../../../../Clients/crm-web/src/app/employees/application/employee-state.service';
import { Employee } from '../../../../Clients/crm-web/src/app/employees/domain/employee.model';

describe('EmployeeListComponent', () => {
  const employees: Employee[] = [
    {
      id: 'e1',
      firstName: 'Max',
      lastName: 'Muster',
      email: 'max@example.com',
      role: 'Employee',
      isActive: true,
      services: [{ serviceId: 's1', serviceName: 'Beratung' }],
      workingHours: [],
      absences: [],
      createdAt: '2024-01-01T00:00:00Z',
      createdBy: 'tester',
      modifiedAt: null,
      modifiedBy: null,
    },
  ];

  function setup() {
    const stateMock = {
      employees: () => employees,
      totalCount: () => 1,
      page: () => 1,
      searchTerm: () => '',
      roleFilter: () => null,
      serviceIdFilter: () => null,
      loading: () => false,
      hasErrors: () => false,
      errors: () => [],
      loadEmployees: vi.fn(),
      setSearchTerm: vi.fn(),
      setRoleFilter: vi.fn(),
      setServiceIdFilter: vi.fn(),
      setPage: vi.fn(),
      deactivateEmployee: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [EmployeeListComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: EmployeeStateService, useValue: stateMock },
      ],
    });

    const fixture = TestBed.createComponent(EmployeeListComponent);
    fixture.detectChanges();
    return { fixture, stateMock, component: fixture.componentInstance };
  }

  it('loads employees on init', () => {
    const { stateMock } = setup();
    expect(stateMock.loadEmployees).toHaveBeenCalled();
  });

  it('renders the employee table with role and services', () => {
    const { fixture } = setup();
    const html = fixture.nativeElement.textContent as string;
    expect(html).toContain('Muster');
    expect(html).toContain('Beratung');
  });

  it('filters by role when the role select changes', () => {
    const { component, stateMock } = setup();
    component.onRoleChange('Admin');
    expect(stateMock.setRoleFilter).toHaveBeenCalledWith('Admin');
  });

  it('filters by service id when the service filter changes', () => {
    const { component, stateMock } = setup();
    component.onServiceIdChange('s1');
    expect(stateMock.setServiceIdFilter).toHaveBeenCalledWith('s1');
  });

  it('deactivates the employee when the deactivate action is triggered', () => {
    const { component, stateMock } = setup();
    component.deactivate('e1');
    expect(stateMock.deactivateEmployee).toHaveBeenCalledWith('e1', expect.any(Function));
  });
});
