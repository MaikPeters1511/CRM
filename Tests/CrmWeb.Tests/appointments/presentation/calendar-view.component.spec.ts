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

  function setup(role: 'Admin' | 'Employee' = 'Admin', absenceEntries: unknown[] = []) {
    const stateMock = {
      viewMode: () => 'week' as const,
      anchorDate: () => '2024-05-15',
      employeeFilter: () => null as string | null,
      appointments: () => [appointment],
      absenceEntries: () => absenceEntries,
      loading: () => false,
      errors: () => [],
      hasErrors: () => false,
      conflict: () => false,
      range: () => ({ from: '2024-05-13', to: '2024-05-19' }),
      setViewMode: vi.fn(),
      setAnchorDate: vi.fn(),
      setEmployeeFilter: vi.fn(),
      shift: vi.fn(),
      goToToday: vi.fn(),
      loadAppointments: vi.fn(),
    };
    const employeeApiMock = {
      search: vi.fn().mockReturnValue(
        of({ items: [{ id: 'e2', firstName: 'Erika', lastName: 'Mustermann', isActive: true, services: [] }], totalCount: 1, page: 1, pageSize: 20 }),
      ),
    };
    const authMock = { isAdmin: () => role === 'Admin', currentUser: () => ({ userId: 'e1', role, displayName: 'Ich' }) };

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

  it('navigates to the prefilled appointment form when an empty hour cell is opened (Bug: leere Zellen waren wirkungslos)', () => {
    const { component, routerMock } = setup();
    const cell = { label: '14:00', dateIso: '2024-05-16', hour: 14, appointments: [] };

    component.openCell(cell);

    expect(routerMock.navigate).toHaveBeenCalledWith(['/appointments/new'], {
      queryParams: { date: '2024-05-16', startTime: '14:00' },
    });
  });

  it('includes the active employee filter as employeeId query param when opening an empty cell', () => {
    const { component, stateMock, routerMock } = setup();
    stateMock.employeeFilter = () => 'e2';
    const cell = { label: '14:00', dateIso: '2024-05-16', hour: 14, appointments: [] };

    component.openCell(cell);

    expect(routerMock.navigate).toHaveBeenCalledWith(['/appointments/new'], {
      queryParams: { date: '2024-05-16', startTime: '14:00', employeeId: 'e2' },
    });
  });

  it('drills into the day view instead of navigating when an empty month-view day cell is opened', () => {
    const { component, stateMock, routerMock } = setup();
    const cell = { label: '16', dateIso: '2024-05-16', hour: null, appointments: [] };

    component.openCell(cell);

    expect(stateMock.setViewMode).toHaveBeenCalledWith('day');
    expect(stateMock.setAnchorDate).toHaveBeenCalledWith('2024-05-16');
    expect(routerMock.navigate).not.toHaveBeenCalled();
  });

  it('shows a hover/focus hint on empty cells inviting the user to create an appointment there', () => {
    const { fixture } = setup();
    const hints = fixture.nativeElement.querySelectorAll('.badge-primary.badge-soft');
    expect(
      Array.from(hints).some((hint) => (hint as HTMLElement).textContent?.includes('appointment.calendar.createHere')),
    ).toBe(true);
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

  it('loads the default employee list on init via the real server-side search, without a pageSize override (Bug 10 Nachfolge-Fix)', () => {
    const { employeeApiMock } = setup('Admin');
    const callArgs = employeeApiMock.search.mock.calls[0][0];
    expect(callArgs).toEqual({ search: null, page: 1 });
  });

  it('debounces the employee filter search input (300ms) and queries the server with the typed term', () => {
    vi.useFakeTimers();
    const { component, employeeApiMock } = setup('Admin');
    employeeApiMock.search.mockClear();

    component.onEmployeeFilterQueryInput('Neu');
    TestBed.tick();
    vi.advanceTimersByTime(299);
    expect(employeeApiMock.search).not.toHaveBeenCalled();

    vi.advanceTimersByTime(1);
    expect(employeeApiMock.search).toHaveBeenCalledWith({ search: 'Neu', page: 1 });

    vi.useRealTimers();
  });

  it('makes an employee found by the search term available in the employee filter, even if it would be beyond the default page (e.g. totalCount 37) (Bug 10 Nachfolge-Fix)', () => {
    vi.useFakeTimers();
    const employeeOnPage2 = { id: 'e21', firstName: 'Neuer', lastName: 'Mitarbeiter', isActive: true, services: [] };
    const { component, employeeApiMock } = setup('Admin');
    employeeApiMock.search.mockReturnValue(
      of({ items: [employeeOnPage2], totalCount: 37, page: 1, pageSize: 20 }),
    );

    component.onEmployeeFilterQueryInput(employeeOnPage2.lastName);
    TestBed.tick();
    vi.advanceTimersByTime(300);

    expect(employeeApiMock.search).toHaveBeenCalledWith({ search: employeeOnPage2.lastName, page: 1 });
    expect(component.employees()).toContainEqual(employeeOnPage2);

    vi.useRealTimers();
  });

  it('hides the employee filter dropdown for Employee (RBAC: server liefert ohnehin nur eigene Termine)', () => {
    const { fixture, employeeApiMock } = setup('Employee');
    expect(fixture.nativeElement.querySelector('#employee-filter')).toBeFalsy();
    expect(employeeApiMock.search).not.toHaveBeenCalled();
  });

  it('renders an approved absence entry as a separate, visually distinct full-day badge (Scenario: Genehmigte Abwesenheit erscheint im Kalender)', () => {
    const { fixture } = setup('Admin', [{ employeeId: 'e2', type: 'Vacation', from: '2024-05-14', to: '2024-05-16' }]);

    const badges = fixture.nativeElement.querySelectorAll('.absence-entry');
    expect(badges.length).toBeGreaterThan(0);
    expect((badges[0] as HTMLElement).className).toContain('badge-warning');
  });

  it('shows the employee name and absence type in the absence badge tooltip', () => {
    const { fixture } = setup('Admin', [{ employeeId: 'e2', type: 'Vacation', from: '2024-05-14', to: '2024-05-16' }]);

    const badge = fixture.nativeElement.querySelector('.absence-entry') as HTMLElement;
    expect(badge.title).toContain('Erika');
    expect(badge.title).toContain('Vacation');
  });

  it('does not render an absence badge for days outside the approved absence range', () => {
    const { fixture } = setup('Admin', [{ employeeId: 'e2', type: 'Vacation', from: '2024-01-01', to: '2024-01-02' }]);

    expect(fixture.nativeElement.querySelector('.absence-entry')).toBeFalsy();
  });
});
