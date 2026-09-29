import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { CalendarStateService } from '../../../Clients/appointment-web/src/app/application/calendar-state.service';
import { AppointmentApiService } from '../../../Clients/appointment-web/src/app/infrastructure/appointment-api.service';
import { Appointment, AppointmentListItem } from '../../../Clients/appointment-web/src/app/domain/appointment.model';

/**
 * Deckt die Requirements aus terminverwaltung.openspec.md Abschnitt 3 ab:
 * Termin anlegen (im Zeitfenster / Kollision), Verschieben (Erfolg / Konflikt-Rollback),
 * Stornieren (Erfolg / Rollback) sowie die Wochenansicht (Abschnitt 4).
 */
describe('CalendarStateService', () => {
  const confirmedAppointment: AppointmentListItem = {
    id: 'a1',
    customerId: 'c1',
    employeeId: 'e1',
    serviceId: 's1',
    date: '2024-05-15',
    startTime: '10:00:00',
    endTime: '11:00:00',
    start: '2024-05-15T10:00:00+00:00',
    end: '2024-05-15T11:00:00+00:00',
    status: 'Confirmed',
    cancellationReason: null,
  };

  const fullAppointment: Appointment = {
    id: 'a1',
    customerId: 'c1',
    employeeId: 'e1',
    serviceId: 's1',
    start: '2024-05-15T10:00:00+00:00',
    end: '2024-05-15T11:00:00+00:00',
    status: 'Confirmed',
    cancellationReason: null,
    createdAt: '2024-05-01T00:00:00+00:00',
    createdBy: 'tester',
    modifiedAt: null,
    modifiedBy: null,
  };

  function createApiMock() {
    return {
      getCalendar: vi.fn().mockReturnValue(of([confirmedAppointment])),
      getById: vi.fn().mockReturnValue(of(fullAppointment)),
      create: vi.fn().mockReturnValue(of(fullAppointment)),
      reschedule: vi.fn().mockReturnValue(of(fullAppointment)),
      cancel: vi.fn().mockReturnValue(of({ ...fullAppointment, status: 'Cancelled', cancellationReason: 'Kunde krank' })),
    };
  }

  function setup(apiOverrides: Partial<ReturnType<typeof createApiMock>> = {}) {
    const apiMock = { ...createApiMock(), ...apiOverrides };
    TestBed.configureTestingModule({
      providers: [CalendarStateService, { provide: AppointmentApiService, useValue: apiMock }],
    });
    return { service: TestBed.inject(CalendarStateService), apiMock };
  }

  it('loads the week view with appointments positioned by date/time (Wochenansicht)', () => {
    const { service, apiMock } = setup();

    service.setViewMode('week');
    service.setAnchorDate('2024-05-15');

    expect(apiMock.getCalendar).toHaveBeenCalledWith(
      expect.objectContaining({ from: '2024-05-13', to: '2024-05-19' }),
    );
    expect(service.appointments()).toEqual([confirmedAppointment]);
  });

  it('creates an appointment within an available time window (Scenario: Termin im verfuegbaren Zeitfenster)', () => {
    const { service, apiMock } = setup();
    const onSuccess = vi.fn();

    service.createAppointment(
      { customerId: 'c1', employeeId: 'e1', serviceId: 's1', start: '2024-05-15T10:00:00', end: '2024-05-15T11:00:00' },
      onSuccess,
    );

    expect(apiMock.create).toHaveBeenCalled();
    expect(onSuccess).toHaveBeenCalledWith(fullAppointment);
    expect(service.appointments()).toContainEqual(expect.objectContaining({ id: 'a1', status: 'Confirmed' }));
    expect(service.hasErrors()).toBe(false);
  });

  it('rejects appointment creation on a collision and surfaces a conflict error (Scenario: Doppelbuchung verhindern)', () => {
    const conflictError = new HttpErrorResponse({ status: 409, error: { errors: ['Der Mitarbeiter ist im gewaehlten Zeitraum bereits gebucht.'] } });
    const { service, apiMock } = setup({ create: vi.fn().mockReturnValue(throwError(() => conflictError)) });
    const onSuccess = vi.fn();

    service.createAppointment(
      { customerId: 'c1', employeeId: 'e1', serviceId: 's1', start: '2024-05-15T10:30:00', end: '2024-05-15T11:30:00' },
      onSuccess,
    );

    expect(onSuccess).not.toHaveBeenCalled();
    expect(service.conflict()).toBe(true);
    expect(service.errors()).toEqual(['Der Mitarbeiter ist im gewaehlten Zeitraum bereits gebucht.']);
    expect(service.appointments()).toEqual([]);
  });

  it('rejects appointment creation outside working hours with a validation error (Scenario: Termin ausserhalb der Arbeitszeit)', () => {
    const validationError = new HttpErrorResponse({
      status: 400,
      error: { errors: { '': ['Der Termin liegt ausserhalb der Arbeitszeiten des Mitarbeiters.'] } },
    });
    const { service } = setup({ create: vi.fn().mockReturnValue(throwError(() => validationError)) });
    const onSuccess = vi.fn();

    service.createAppointment(
      { customerId: 'c1', employeeId: 'e1', serviceId: 's1', start: '2024-05-15T22:00:00', end: '2024-05-15T23:00:00' },
      onSuccess,
    );

    expect(onSuccess).not.toHaveBeenCalled();
    expect(service.conflict()).toBe(false);
    expect(service.errors()).toEqual(['Der Termin liegt ausserhalb der Arbeitszeiten des Mitarbeiters.']);
    expect(service.appointments()).toEqual([]);
  });

  it('reschedules an appointment to a collision-free window (Scenario: Termin erfolgreich verschieben)', () => {
    const rescheduled: Appointment = { ...fullAppointment, start: '2024-05-16T09:00:00+00:00', end: '2024-05-16T10:00:00+00:00' };
    const { service, apiMock } = setup({ reschedule: vi.fn().mockReturnValue(of(rescheduled)) });
    service.setAnchorDate('2024-05-15');
    const onSuccess = vi.fn();

    service.rescheduleAppointment('a1', { newStart: '2024-05-16T09:00:00', newEnd: '2024-05-16T10:00:00' }, onSuccess);

    expect(apiMock.reschedule).toHaveBeenCalledWith('a1', { newStart: '2024-05-16T09:00:00', newEnd: '2024-05-16T10:00:00' });
    expect(onSuccess).toHaveBeenCalledWith(rescheduled);
    expect(service.appointments()[0].date).toBe('2024-05-16');
  });

  it('rolls back an optimistic reschedule when the backend reports a conflict (409)', () => {
    const conflictError = new HttpErrorResponse({ status: 409, error: { errors: ['Konflikt mit bestehendem Termin.'] } });
    const { service } = setup({ reschedule: vi.fn().mockReturnValue(throwError(() => conflictError)) });
    service.setAnchorDate('2024-05-15');

    service.rescheduleAppointment('a1', { newStart: '2024-05-15T10:30:00', newEnd: '2024-05-15T11:30:00' }, vi.fn());

    expect(service.conflict()).toBe(true);
    expect(service.appointments()[0]).toEqual(confirmedAppointment);
  });

  it('cancels an appointment and marks it as Cancelled without deleting it (Scenario: Termin stornieren)', () => {
    const { service, apiMock } = setup();
    service.setAnchorDate('2024-05-15');
    const onSuccess = vi.fn();

    service.cancelAppointment('a1', 'Kunde krank', onSuccess);

    expect(apiMock.cancel).toHaveBeenCalledWith('a1', { reason: 'Kunde krank' });
    expect(onSuccess).toHaveBeenCalled();
    expect(service.appointments()[0].status).toBe('Cancelled');
  });

  it('rolls back cancellation when the backend call fails', () => {
    const { service } = setup({ cancel: vi.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 }))) });
    service.setAnchorDate('2024-05-15');

    service.cancelAppointment('a1', 'Kunde krank', vi.fn());

    expect(service.appointments()[0].status).toBe('Confirmed');
    expect(service.errors()).toEqual(['appointment.errors.cancelFailed']);
  });

  it('rolls back and shows a clean forbidden error when rescheduling a colleague appointment is rejected with 403 (RBAC, Scenario: Employee versucht Termin eines anderen Mitarbeiters zu verschieben)', () => {
    const forbiddenError = new HttpErrorResponse({ status: 403 });
    const { service } = setup({ reschedule: vi.fn().mockReturnValue(throwError(() => forbiddenError)) });
    service.setAnchorDate('2024-05-15');
    const onSuccess = vi.fn();
    const onForbidden = vi.fn();

    service.rescheduleAppointment(
      'a1',
      { newStart: '2024-05-15T10:30:00', newEnd: '2024-05-15T11:30:00' },
      onSuccess,
      onForbidden,
    );

    expect(onSuccess).not.toHaveBeenCalled();
    expect(onForbidden).toHaveBeenCalled();
    expect(service.conflict()).toBe(false);
    expect(service.errors()).toEqual(['appointment.errors.forbidden']);
    expect(service.appointments()[0]).toEqual(confirmedAppointment);
  });

  it('rolls back and shows a clean forbidden error when cancelling a colleague appointment is rejected with 403 (RBAC, Scenario: Employee versucht Termin eines anderen Mitarbeiters zu stornieren)', () => {
    const forbiddenError = new HttpErrorResponse({ status: 403 });
    const { service } = setup({ cancel: vi.fn().mockReturnValue(throwError(() => forbiddenError)) });
    service.setAnchorDate('2024-05-15');
    const onSuccess = vi.fn();
    const onForbidden = vi.fn();

    service.cancelAppointment('a1', 'Kunde krank', onSuccess, onForbidden);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(onForbidden).toHaveBeenCalled();
    expect(service.appointments()[0]).toEqual(confirmedAppointment);
    expect(service.errors()).toEqual(['appointment.errors.forbidden']);
  });

  it('loads a single appointment by id for the detail view', () => {
    const { service, apiMock } = setup();

    service.loadAppointmentById('a1');

    expect(apiMock.getById).toHaveBeenCalledWith('a1');
    expect(service.selectedAppointment()).toEqual(fullAppointment);
  });
});
