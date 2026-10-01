import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { provideRouter, Router } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { CalendarViewComponent } from '../../../../Clients/crm-web/src/app/appointments/presentation/calendar-view/calendar-view.component';
import { CalendarStateService } from '../../../../Clients/crm-web/src/app/appointments/application/calendar-state.service';
import { AuthStateService } from '../../../../Clients/crm-web/src/app/auth/application/auth-state.service';
import { EmployeeApiService } from '../../../../Clients/crm-web/src/app/appointments/infrastructure/employee-api.service';
import { AppointmentListItem } from '../../../../Clients/crm-web/src/app/appointments/domain/appointment.model';

describe('CalendarViewComponent', () => {
  const appointment: AppointmentListItem = {
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

  function setup(role: 'Admin' | 'Employee' = 'Admin') {
    const stateMock = {
      viewMode: () => 'week' as const,
      anchorDate: () => '2024-05-15',
      employeeFilter: () => null,
      appointments: () => [appointment],
      loading: () => false,
      errors: () => [],
      hasErrors: () => false,
      conflict: () => false,
      range: () => ({ from: '2024-05-13', to: '2024-05-19' }),
      setViewMode: vi.fn(),
      setEmployeeFilter: vi.fn(),
      shift: vi.fn(),
      goToToday: vi.fn(),
      loadAppointments: vi.fn(),
    };
    const employeeApiMock = { search: vi.fn().mockReturnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 20 })) };
    const authMock = { isAdmin: () => role === 'Admin' };

    TestBed.configureTestingModule({
      imports: [CalendarViewComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: CalendarStateService, useValue: stateMock },
        { provide: EmployeeApiService, useValue: employeeApiMock },
        { provide: AuthStateService, useValue: authMock },
      ],
    });

    const fixture = TestBed.createComponent(CalendarViewComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const routerMock = { navigate: vi.spyOn(router, 'navigate').mockResolvedValue(true) };
    return { fixture, stateMock, employeeApiMock, routerMock, component: fixture.componentInstance };
  }

  it('loads appointments for the current range on init', () => {
    const { stateMock } = setup();
    expect(stateMock.loadAppointments).toHaveBeenCalled();
  });

  it('renders the calendar as an accessible grid with gridcells (Kalender-Grid role=grid/gridcell)', () => {
    const { fixture } = setup();
    const grid = fixture.nativeElement.querySelector('[role="grid"]');
    const cells = fixture.nativeElement.querySelectorAll('[role="gridcell"]');
    expect(grid).toBeTruthy();
    expect(cells.length).toBeGreaterThan(0);
  });

  it('switches the view mode via the state service when a tab is selected', () => {
    const { component, stateMock } = setup();
    component.selectViewMode('month');
    expect(stateMock.setViewMode).toHaveBeenCalledWith('month');
  });

  it('navigates to the appointment detail page on Enter for a cell with an appointment', () => {
    const { component, routerMock } = setup();
    const cell = { label: '10:00', dateIso: '2024-05-15', hour: 10, appointments: [appointment] };

    component.openCell(cell);

    expect(routerMock.navigate).toHaveBeenCalledWith(['/appointments', 'a1']);
  });

  it('moves focus between cells on arrow key navigation', () => {
    const { component } = setup();
    const cell = { label: '10:00', dateIso: '2024-05-15', hour: 10, appointments: [] };
    const event = { key: 'ArrowRight', preventDefault: vi.fn() } as unknown as KeyboardEvent;

    component.onGridKeydown(event, 0, 0, cell);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(component.focusedCol()).toBe(1);
  });

  it('shows the employee filter dropdown for Admin (Gesamtuebersicht aller Mitarbeiter)', () => {
    const { fixture, employeeApiMock } = setup('Admin');
    expect(fixture.nativeElement.querySelector('#employee-filter')).toBeTruthy();
    expect(employeeApiMock.search).toHaveBeenCalled();
  });

  it('requests a page size large enough to cover all active employees, not just the default page-1/20 window (Bug 10 fix)', () => {
    const { employeeApiMock } = setup('Admin');
    const callArgs = employeeApiMock.search.mock.calls[0][0];
    expect(callArgs).toEqual(expect.objectContaining({ search: null, page: 1 }));
    expect(callArgs.pageSize).toBeGreaterThan(20);
  });

  it('makes an employee beyond position 20 (e.g. totalCount 37) available in the employee filter (Bug 10 regression)', () => {
    const employeeOnPage2 = { id: 'e21', firstName: 'Neuer', lastName: 'Mitarbeiter', isActive: true, services: [] };
    const stateMock = {
      viewMode: () => 'week' as const,
      anchorDate: () => '2024-05-15',
      employeeFilter: () => null,
      appointments: () => [appointment],
      loading: () => false,
      errors: () => [],
      hasErrors: () => false,
      conflict: () => false,
      range: () => ({ from: '2024-05-13', to: '2024-05-19' }),
      setViewMode: vi.fn(),
      setEmployeeFilter: vi.fn(),
      shift: vi.fn(),
      goToToday: vi.fn(),
      loadAppointments: vi.fn(),
    };
    const employeeApiMock = {
      search: vi.fn().mockReturnValue(of({ items: [employeeOnPage2], totalCount: 37, page: 1, pageSize: 200 })),
    };
    const authMock = { isAdmin: () => true };
    TestBed.configureTestingModule({
      imports: [CalendarViewComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: CalendarStateService, useValue: stateMock },
        { provide: EmployeeApiService, useValue: employeeApiMock },
        { provide: AuthStateService, useValue: authMock },
      ],
    });
    const fixture = TestBed.createComponent(CalendarViewComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.employees()).toContainEqual(employeeOnPage2);
  });

  it('hides the employee filter dropdown for Employee (RBAC: server liefert ohnehin nur eigene Termine)', () => {
    const { fixture, employeeApiMock } = setup('Employee');
    expect(fixture.nativeElement.querySelector('#employee-filter')).toBeFalsy();
    expect(employeeApiMock.search).not.toHaveBeenCalled();
  });
});
