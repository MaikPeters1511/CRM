import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { EmployeeFormComponent } from '../../../../Clients/crm-web/src/app/employees/presentation/employee-form/employee-form.component';
import { EmployeeStateService } from '../../../../Clients/crm-web/src/app/employees/application/employee-state.service';
import { ServiceLookupApiService } from '../../../../Clients/crm-web/src/app/employees/infrastructure/service-lookup-api.service';

describe('EmployeeFormComponent', () => {
  const activeServices = [
    { id: 's1', name: 'Beratung' },
    { id: 's2', name: 'Haarschnitt' },
  ];

  function setup(routeParamId: string | null = null, selectedEmployeeServices: { serviceId: string; serviceName: string }[] = []) {
    const stateMock = {
      selectedEmployee: () =>
        routeParamId ? { id: routeParamId, services: selectedEmployeeServices } : null,
      hasErrors: () => false,
      errors: () => [],
      loadEmployeeById: vi.fn(),
      createEmployee: vi.fn(),
      updateEmployee: vi.fn(),
    };
    const serviceLookupMock = {
      getActiveServices: vi.fn().mockReturnValue(of(activeServices)),
    };

    TestBed.configureTestingModule({
      imports: [EmployeeFormComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: EmployeeStateService, useValue: stateMock },
        { provide: ServiceLookupApiService, useValue: serviceLookupMock },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => routeParamId } } },
        },
      ],
    });

    const fixture = TestBed.createComponent(EmployeeFormComponent);
    fixture.detectChanges();
    return { fixture, stateMock, serviceLookupMock, component: fixture.componentInstance };
  }

  it('shows validation errors and does not save when mandatory fields are missing', () => {
    const { fixture, stateMock, component } = setup();

    component.save();
    fixture.detectChanges();

    expect(stateMock.createEmployee).not.toHaveBeenCalled();
    expect(component['form'].invalid).toBe(true);
    const firstNameError = fixture.nativeElement.querySelector('#firstName-error');
    expect(firstNameError).toBeTruthy();
  });

  it('creates the employee with role when all mandatory fields including role are valid', () => {
    const { stateMock, component } = setup();
    component['form'].patchValue({
      firstName: 'Max',
      lastName: 'Muster',
      email: 'max@example.com',
      role: 'Admin',
    });

    component.save();

    expect(stateMock.createEmployee).toHaveBeenCalledWith(
      expect.objectContaining({ firstName: 'Max', lastName: 'Muster', email: 'max@example.com', role: 'Admin' }),
      expect.any(Function),
    );
  });

  it('loads the active service catalog for the assignment select (GET /api/services?status=true)', () => {
    const { serviceLookupMock, component } = setup();

    expect(serviceLookupMock.getActiveServices).toHaveBeenCalled();
    expect(component['availableServices']()).toEqual(activeServices);
  });

  it('adds a service assignment selected from the catalog and includes it when updating the employee', () => {
    const { stateMock, component } = setup('e1');

    component['newServiceForm'].patchValue({ selectedServiceId: 's1' });
    component.addService();

    expect(component['services']()).toEqual([{ serviceId: 's1', serviceName: 'Beratung' }]);

    component['form'].patchValue({
      firstName: 'Erika',
      lastName: 'Mustermann',
      email: 'erika@example.com',
      role: 'Employee',
    });
    component.save();

    expect(stateMock.updateEmployee).toHaveBeenCalledWith(
      'e1',
      expect.objectContaining({ services: [{ serviceId: 's1', serviceName: 'Beratung' }] }),
      expect.any(Function),
    );
  });

  it('removes a service assignment', () => {
    const { component } = setup('e1');
    component['newServiceForm'].patchValue({ selectedServiceId: 's1' });
    component.addService();

    component.removeService('s1');

    expect(component['services']()).toEqual([]);
  });

  it('keeps an already assigned, now deactivated service visible in the list (Snapshot-Verhalten)', () => {
    const { component } = setup('e1', [{ serviceId: 'deactivated-1', serviceName: 'Alte Leistung' }]);

    expect(component['services']()).toEqual([{ serviceId: 'deactivated-1', serviceName: 'Alte Leistung' }]);
    // Die deaktivierte Leistung erscheint nicht in der Auswahlliste des Katalogs (nur aktive Leistungen werden geladen).
    expect(component['availableServices']().some((s: { id: string }) => s.id === 'deactivated-1')).toBe(false);
  });

  it('excludes already assigned services from the selectable options', () => {
    const { component } = setup('e1');

    component['newServiceForm'].patchValue({ selectedServiceId: 's1' });
    component.addService();

    expect(component['selectableServices']()).toEqual([{ id: 's2', name: 'Haarschnitt' }]);
  });
});
