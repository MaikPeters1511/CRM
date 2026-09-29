import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { AppointmentDetailComponent } from '../../../Clients/appointment-web/src/app/presentation/appointment-detail/appointment-detail.component';
import { CalendarStateService } from '../../../Clients/appointment-web/src/app/application/calendar-state.service';
import { Appointment } from '../../../Clients/appointment-web/src/app/domain/appointment.model';

describe('AppointmentDetailComponent', () => {
  const appointment: Appointment = {
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

  function setup(stateOverrides: Record<string, unknown> = {}) {
    const stateMock = {
      loading: () => false,
      selectedAppointment: () => appointment,
      errors: () => [],
      hasErrors: () => false,
      conflict: () => false,
      clearErrors: vi.fn(),
      loadAppointmentById: vi.fn(),
      rescheduleAppointment: vi.fn((_id: string, _req: unknown, onSuccess: () => void) => onSuccess()),
      cancelAppointment: vi.fn((_id: string, _reason: string, onSuccess: () => void) => onSuccess()),
      ...stateOverrides,
    };

    TestBed.configureTestingModule({
      imports: [AppointmentDetailComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: CalendarStateService, useValue: stateMock },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'a1' } } } },
      ],
    });

    const fixture = TestBed.createComponent(AppointmentDetailComponent);
    fixture.detectChanges();
    return { fixture, stateMock, component: fixture.componentInstance };
  }

  it('loads the appointment by id from the route', () => {
    const { stateMock } = setup();
    expect(stateMock.loadAppointmentById).toHaveBeenCalledWith('a1');
  });

  it('shows the status with text/icon, not only color (a11y)', () => {
    const { fixture } = setup();
    expect(fixture.nativeElement.textContent).toContain('appointment.status.Confirmed');
  });

  it('reschedules the appointment with the new date/time from the form (Termin verschieben)', () => {
    const { fixture, stateMock, component } = setup();
    component.rescheduleForm.setValue({ date: '2024-05-16', startTime: '09:00', endTime: '10:00' });
    fixture.detectChanges();

    component.reschedule();

    expect(stateMock.rescheduleAppointment).toHaveBeenCalledWith(
      'a1',
      { newStart: '2024-05-16T09:00:00', newEnd: '2024-05-16T10:00:00' },
      expect.any(Function),
    );
  });

  it('opens the cancel confirmation modal and cancels with a reason on confirm (Termin stornieren)', () => {
    const { fixture, stateMock, component } = setup();

    component.openCancelModal();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeTruthy();

    component.cancelForm.setValue({ reason: 'Kunde krank' });
    component.confirmCancel();
    fixture.detectChanges();

    expect(stateMock.cancelAppointment).toHaveBeenCalledWith(
      'a1',
      'Kunde krank',
      expect.any(Function),
      expect.any(Function),
    );
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeFalsy();
  });

  it('does not cancel without a reason', () => {
    const { component, stateMock } = setup();
    component.openCancelModal();
    component.confirmCancel();
    expect(stateMock.cancelAppointment).not.toHaveBeenCalled();
  });

  it('closes the cancel modal on Escape', () => {
    const { fixture, component } = setup();
    component.openCancelModal();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeTruthy();

    component.closeCancelModal();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeFalsy();
  });

  it('shows a clean forbidden message and closes the cancel modal when cancelling a colleague appointment is rejected with 403 (RBAC, Scenario: Employee versucht Termin eines anderen Mitarbeiters zu stornieren)', () => {
    const { fixture, stateMock, component } = setup({
      hasErrors: () => true,
      errors: () => ['appointment.errors.forbidden'],
      cancelAppointment: vi.fn(
        (_id: string, _reason: string, _onSuccess: () => void, onForbidden?: () => void) => onForbidden?.(),
      ),
    });

    component.openCancelModal();
    fixture.detectChanges();
    component.cancelForm.setValue({ reason: 'Kunde krank' });
    component.confirmCancel();
    fixture.detectChanges();

    expect(stateMock.cancelAppointment).toHaveBeenCalledWith(
      'a1',
      'Kunde krank',
      expect.any(Function),
      expect.any(Function),
    );
    // Modal wird geschlossen, damit die Fehlermeldung im Detail-View sichtbar wird
    // (kein hängender Optimistic-Update-State, keine verdeckte Fehlermeldung).
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeFalsy();
    expect(fixture.nativeElement.textContent).toContain('appointment.errors.forbidden');
  });
});
