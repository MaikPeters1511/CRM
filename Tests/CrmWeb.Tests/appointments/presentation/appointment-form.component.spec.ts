import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { provideRouter, Router } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { AppointmentFormComponent } from '../../../../Clients/crm-web/src/app/appointments/presentation/appointment-form/appointment-form.component';
import { CalendarStateService } from '../../../../Clients/crm-web/src/app/appointments/application/calendar-state.service';
import { CustomerApiService } from '../../../../Clients/crm-web/src/app/appointments/infrastructure/customer-api.service';
import { EmployeeApiService } from '../../../../Clients/crm-web/src/app/appointments/infrastructure/employee-api.service';

describe('AppointmentFormComponent', () => {
  const employee = {
    id: 'e1',
    firstName: 'Erika',
    lastName: 'Mitarbeiter',
    isActive: true,
    services: [{ serviceId: 's1', serviceName: 'Haarschnitt' }],
  };

  function setup() {
    const stateMock = {
      hasErrors: () => false,
      errors: () => [],
      conflict: () => false,
      clearErrors: vi.fn(),
      createAppointment: vi.fn((_req: unknown, onSuccess: (a: unknown) => void) => onSuccess({ id: 'a1' })),
    };
    const customerApiMock = { search: vi.fn().mockReturnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 20 })) };
    const employeeApiMock = {
      search: vi.fn().mockReturnValue(of({ items: [employee], totalCount: 1, page: 1, pageSize: 20 })),
      getAvailability: vi.fn().mockReturnValue(of([{ date: '2024-05-15', startTime: '09:00:00', endTime: '10:00:00' }])),
    };
    TestBed.configureTestingModule({
      imports: [AppointmentFormComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: CalendarStateService, useValue: stateMock },
        { provide: CustomerApiService, useValue: customerApiMock },
        { provide: EmployeeApiService, useValue: employeeApiMock },
      ],
    });

    const fixture = TestBed.createComponent(AppointmentFormComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const routerMock = { navigateByUrl: vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true) };
    return { fixture, stateMock, employeeApiMock, routerMock, component: fixture.componentInstance };
  }

  it('loads active employees on init for the employee selection', () => {
    const { component } = setup();
    expect(component.employees()).toEqual([employee]);
  });

  it('offers the selected employee services for the service dropdown', () => {
    const { component } = setup();
    component.form.controls.employeeId.setValue('e1');
    expect(component.selectedEmployeeServices()).toEqual(employee.services);
  });

  it('loads live availability when employee and date are selected', () => {
    const { component, employeeApiMock } = setup();
    component.onEmployeeChange('e1');
    component.onDateChange('2024-05-15');

    expect(employeeApiMock.getAvailability).toHaveBeenCalledWith('e1', '2024-05-15', '2024-05-15');
    expect(component.availableSlots()).toEqual([{ date: '2024-05-15', startTime: '09:00:00', endTime: '10:00:00' }]);
  });

  it('applies a suggested slot to the start/end time fields', () => {
    const { component } = setup();
    component.applySlot({ date: '2024-05-15', startTime: '09:00:00', endTime: '10:00:00' });

    expect(component.form.controls.startTime.value).toBe('09:00');
    expect(component.form.controls.endTime.value).toBe('10:00');
  });

  it('submits a create-appointment request built from the form values', () => {
    const { component, stateMock } = setup();
    component.form.setValue({
      customerId: 'c1',
      employeeId: 'e1',
      serviceId: 's1',
      date: '2024-05-15',
      startTime: '10:00',
      endTime: '11:00',
    });

    component.submit();

    expect(stateMock.createAppointment).toHaveBeenCalledWith(
      {
        customerId: 'c1',
        employeeId: 'e1',
        serviceId: 's1',
        start: '2024-05-15T10:00:00',
        end: '2024-05-15T11:00:00',
      },
      expect.any(Function),
    );
  });

  it('does not submit an invalid (incomplete) form', () => {
    const { component, stateMock } = setup();
    component.submit();
    expect(stateMock.createAppointment).not.toHaveBeenCalled();
  });
});
