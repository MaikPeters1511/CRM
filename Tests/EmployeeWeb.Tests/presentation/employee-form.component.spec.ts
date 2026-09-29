import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { EmployeeFormComponent } from '../../../Clients/employee-web/src/app/presentation/employee-form/employee-form.component';
import { EmployeeStateService } from '../../../Clients/employee-web/src/app/application/employee-state.service';

describe('EmployeeFormComponent', () => {
  function setup(routeParamId: string | null = null) {
    const stateMock = {
      selectedEmployee: () => null,
      hasErrors: () => false,
      errors: () => [],
      loadEmployeeById: vi.fn(),
      createEmployee: vi.fn(),
      updateEmployee: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [EmployeeFormComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: EmployeeStateService, useValue: stateMock },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => routeParamId } } },
        },
      ],
    });

    const fixture = TestBed.createComponent(EmployeeFormComponent);
    fixture.detectChanges();
    return { fixture, stateMock, component: fixture.componentInstance };
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

  it('adds a service assignment and includes it when updating the employee', () => {
    const { stateMock, component } = setup('e1');

    component['newServiceForm'].patchValue({ serviceId: 's1', serviceName: 'Beratung' });
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
    component['newServiceForm'].patchValue({ serviceId: 's1', serviceName: 'Beratung' });
    component.addService();

    component.removeService('s1');

    expect(component['services']()).toEqual([]);
  });
});
