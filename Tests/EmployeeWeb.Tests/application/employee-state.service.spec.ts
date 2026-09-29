import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { EmployeeStateService } from '../../../Clients/employee-web/src/app/application/employee-state.service';
import { EmployeeApiService } from '../../../Clients/employee-web/src/app/infrastructure/employee-api.service';
import { Employee } from '../../../Clients/employee-web/src/app/domain/employee.model';

/**
 * Deckt die Requirements aus Abschnitt 3 der Change-Spec ab:
 * Anlegen (gueltig/E-Mail-Konflikt), Leistung zuordnen, Arbeitszeit hinterlegen,
 * Abwesenheit inkl. Konfliktfall, Deaktivieren.
 */
describe('EmployeeStateService', () => {
  const activeEmployee: Employee = {
    id: 'e1',
    firstName: 'Erika',
    lastName: 'Mustermann',
    email: 'erika@example.com',
    role: 'Employee',
    isActive: true,
    services: [],
    workingHours: [],
    absences: [],
    createdAt: '2024-01-01T00:00:00Z',
    createdBy: 'tester',
    modifiedAt: null,
    modifiedBy: null,
  };

  function createApiMock() {
    return {
      search: vi.fn().mockReturnValue(of({ items: [activeEmployee], totalCount: 1, page: 1, pageSize: 20 })),
      getById: vi.fn().mockReturnValue(of(activeEmployee)),
      create: vi.fn().mockReturnValue(of(activeEmployee)),
      update: vi.fn().mockReturnValue(of(activeEmployee)),
      deactivate: vi.fn().mockReturnValue(of(undefined)),
      setSchedule: vi.fn().mockReturnValue(of(activeEmployee)),
      addAbsence: vi.fn().mockReturnValue(of({ id: 'a1', type: 'Vacation', from: '2026-10-15', to: '2026-10-20' })),
    };
  }

  function setup(apiOverrides: Partial<ReturnType<typeof createApiMock>> = {}) {
    const apiMock = { ...createApiMock(), ...apiOverrides };

    TestBed.configureTestingModule({
      providers: [EmployeeStateService, { provide: EmployeeApiService, useValue: apiMock }],
    });

    return {
      service: TestBed.inject(EmployeeStateService),
      apiMock,
    };
  }

  it('loads employees matching the search term', () => {
    const { service, apiMock } = setup();

    service.setSearchTerm('Muster');

    expect(apiMock.search).toHaveBeenCalledWith(
      expect.objectContaining({ search: 'Muster', page: 1 }),
    );
    expect(service.employees()).toEqual([activeEmployee]);
    expect(service.totalCount()).toBe(1);
  });

  it('filters employees by role and service', () => {
    const { service, apiMock } = setup();

    service.setRoleFilter('Admin');
    expect(apiMock.search).toHaveBeenCalledWith(expect.objectContaining({ role: 'Admin' }));

    service.setServiceIdFilter('s1');
    expect(apiMock.search).toHaveBeenCalledWith(expect.objectContaining({ serviceId: 's1' }));
  });

  it('creates an employee with valid mandatory data (Mitarbeiter mit gueltigen Pflichtdaten anlegen)', () => {
    const { service, apiMock } = setup();
    const onSuccess = vi.fn();

    service.createEmployee({ firstName: 'Max', lastName: 'Muster', email: 'max@example.com', role: 'Employee' }, onSuccess);

    expect(apiMock.create).toHaveBeenCalled();
    expect(onSuccess).toHaveBeenCalledWith(activeEmployee);
    expect(service.employees()[0]).toEqual(activeEmployee);
    expect(service.hasErrors()).toBe(false);
  });

  it('surfaces a conflict error and does not persist when the email is already taken', () => {
    const conflictError = new HttpErrorResponse({
      status: 409,
      error: { errors: ['Email is already assigned to another employee.'] },
    });
    const { service, apiMock } = setup({ create: vi.fn().mockReturnValue(throwError(() => conflictError)) });
    const onSuccess = vi.fn();

    service.createEmployee({ firstName: 'Max', lastName: 'Muster', email: 'max@example.com', role: 'Employee' }, onSuccess);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(service.employees()).toEqual([]);
    expect(service.errors()).toEqual(['Email is already assigned to another employee.']);
  });

  it('assigns a service to an employee (Leistung zuordnen)', () => {
    const updated: Employee = { ...activeEmployee, services: [{ serviceId: 's1', serviceName: 'Beratung' }] };
    const { service, apiMock } = setup({ update: vi.fn().mockReturnValue(of(updated)) });
    const onSuccess = vi.fn();

    service.updateEmployee(
      'e1',
      { firstName: 'Erika', lastName: 'Mustermann', email: 'erika@example.com', role: 'Employee', services: [{ serviceId: 's1', serviceName: 'Beratung' }] },
      onSuccess,
    );

    expect(apiMock.update).toHaveBeenCalledWith('e1', expect.objectContaining({ services: [{ serviceId: 's1', serviceName: 'Beratung' }] }));
    expect(onSuccess).toHaveBeenCalledWith(updated);
  });

  it('rolls back the optimistic update when saving fails', () => {
    const { service, apiMock } = setup();
    service.setSearchTerm('');
    apiMock.update.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

    service.updateEmployee(
      'e1',
      { firstName: 'Kaputt', lastName: 'Mustermann', email: 'erika@example.com', role: 'Employee', services: null },
      vi.fn(),
    );

    expect(service.employees()[0].firstName).toBe('Erika');
    expect(service.errors()).toEqual(['employee.errors.saveFailed']);
  });

  it('sets weekly working hours (Wochenarbeitszeit hinterlegen)', () => {
    const withSchedule: Employee = {
      ...activeEmployee,
      workingHours: [{ weekday: 'Monday', startTime: '09:00:00', endTime: '17:00:00' }],
    };
    const { service, apiMock } = setup({ setSchedule: vi.fn().mockReturnValue(of(withSchedule)) });
    const onSuccess = vi.fn();

    service.setWorkingHours('e1', [{ weekday: 'Monday', startTime: '09:00:00', endTime: '17:00:00' }], onSuccess);

    expect(apiMock.setSchedule).toHaveBeenCalledWith('e1', { slots: [{ weekday: 'Monday', startTime: '09:00:00', endTime: '17:00:00' }] });
    expect(onSuccess).toHaveBeenCalledWith(withSchedule);
    expect(service.selectedEmployee()).toEqual(withSchedule);
  });

  it('adds an absence for a conflict-free period (Urlaub eintragen)', () => {
    const { service, apiMock } = setup();
    const onSuccess = vi.fn();

    service.addAbsence('e1', { type: 'Vacation', from: '2026-11-01', to: '2026-11-05', confirmOverride: false }, onSuccess);

    expect(apiMock.addAbsence).toHaveBeenCalledWith('e1', { type: 'Vacation', from: '2026-11-01', to: '2026-11-05', confirmOverride: false });
    expect(onSuccess).toHaveBeenCalled();
    expect(service.hasAbsenceConflict()).toBe(false);
  });

  it('surfaces the conflicting appointments when the absence collides with an existing appointment (Urlaub kollidiert mit bestehendem Termin)', () => {
    const conflictError = new HttpErrorResponse({
      status: 409,
      error: {
        errors: ['The absence period conflicts with existing appointments.'],
        conflictingAppointments: [{ date: '2026-10-15', startTime: '10:00:00', endTime: '11:00:00' }],
      },
    });
    const { service, apiMock } = setup({ addAbsence: vi.fn().mockReturnValue(throwError(() => conflictError)) });
    const onSuccess = vi.fn();

    service.addAbsence('e1', { type: 'Vacation', from: '2026-10-15', to: '2026-10-15', confirmOverride: false }, onSuccess);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(service.hasAbsenceConflict()).toBe(true);
    expect(service.absenceConflict()).toEqual([{ date: '2026-10-15', startTime: '10:00:00', endTime: '11:00:00' }]);
    expect(service.errors()).toEqual(['employee.absence.errors.conflict']);
  });

  it('retries the absence with confirmOverride=true after the user confirms the conflict', () => {
    const { service, apiMock } = setup();
    const onSuccess = vi.fn();

    service.addAbsence('e1', { type: 'Vacation', from: '2026-10-15', to: '2026-10-15', confirmOverride: true }, onSuccess);

    expect(apiMock.addAbsence).toHaveBeenCalledWith('e1', expect.objectContaining({ confirmOverride: true }));
    expect(onSuccess).toHaveBeenCalled();
  });

  it('deactivates an employee (Mitarbeiter deaktivieren)', () => {
    const { service } = setup();
    service.setSearchTerm('');
    const onSuccess = vi.fn();

    service.deactivateEmployee('e1', onSuccess);

    expect(onSuccess).toHaveBeenCalled();
    expect(service.employees()[0].isActive).toBe(false);
  });

  it('rolls back deactivation when the backend call fails', () => {
    const { service, apiMock } = setup();
    service.setSearchTerm('');
    apiMock.deactivate.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

    service.deactivateEmployee('e1', vi.fn());

    expect(service.employees()[0].isActive).toBe(true);
    expect(service.errors()).toEqual(['employee.errors.deactivateFailed']);
  });

  it('loads a single employee by id for the detail/schedule/absence views', () => {
    const { service, apiMock } = setup();

    service.loadEmployeeById('e1');

    expect(apiMock.getById).toHaveBeenCalledWith('e1');
    expect(service.selectedEmployee()).toEqual(activeEmployee);
  });
});
