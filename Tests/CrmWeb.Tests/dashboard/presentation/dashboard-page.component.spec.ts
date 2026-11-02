import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { DashboardPageComponent } from '../../../../Clients/crm-web/src/app/dashboard/presentation/dashboard-page/dashboard-page.component';
import { DashboardStateService } from '../../../../Clients/crm-web/src/app/dashboard/application/dashboard-state.service';
import { AssistantStateService } from '../../../../Clients/crm-web/src/app/assistant/application/assistant-state.service';
import { UpcomingAppointmentItem } from '../../../../Clients/crm-web/src/app/dashboard/domain/dashboard.model';

describe('DashboardPageComponent', () => {
  const upcoming: UpcomingAppointmentItem[] = [
    { id: 'a1', start: '2024-05-15T10:00:00+00:00', end: '2024-05-15T11:00:00+00:00', status: 'Confirmed' },
    { id: 'a2', start: '2024-05-15T13:00:00+00:00', end: '2024-05-15T14:00:00+00:00', status: 'RequiresReview' },
  ];

  function setup(overrides?: {
    metrics?: ReturnType<typeof metricsFixture>;
    upcomingAppointments?: UpcomingAppointmentItem[];
    loading?: boolean;
    error?: string | null;
  }) {
    const dashboardStateMock = {
      metrics: () => overrides?.metrics ?? metricsFixture(),
      upcomingAppointments: () => overrides?.upcomingAppointments ?? upcoming,
      loading: () => overrides?.loading ?? false,
      error: () => overrides?.error ?? null,
      load: vi.fn(),
    };
    const assistantStateMock = { open: vi.fn() };

    TestBed.configureTestingModule({
      imports: [DashboardPageComponent],
      providers: [
        provideRouter([]),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
        { provide: DashboardStateService, useValue: dashboardStateMock },
        { provide: AssistantStateService, useValue: assistantStateMock },
      ],
    });

    const fixture = TestBed.createComponent(DashboardPageComponent);
    fixture.detectChanges();
    return { fixture, dashboardStateMock, assistantStateMock };
  }

  function metricsFixture() {
    return { appointmentsToday: 3, openRequests: 1, activeCustomers: 42, totalEmployees: 7 };
  }

  it('loads dashboard data on init', () => {
    const { dashboardStateMock } = setup();

    expect(dashboardStateMock.load).toHaveBeenCalled();
  });

  it('renders a metric tile for each available dashboard metric', () => {
    const { fixture } = setup();

    const tiles = fixture.nativeElement.querySelectorAll('[data-testid="dashboard-metric-tile"]');
    expect(tiles.length).toBe(4);
    expect(fixture.nativeElement.textContent).toContain('3');
    expect(fixture.nativeElement.textContent).toContain('42');
  });

  it('shows the upcoming-appointments list sorted as provided by the state service', () => {
    const { fixture } = setup();

    const rows = fixture.nativeElement.querySelectorAll('[data-testid="upcoming-appointment-row"]');
    expect(rows.length).toBe(2);
  });

  it('shows an empty state when there are no upcoming appointments', () => {
    const { fixture } = setup({ upcomingAppointments: [] });

    const rows = fixture.nativeElement.querySelectorAll('[data-testid="upcoming-appointment-row"]');
    expect(rows.length).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('dashboard.upcoming.empty');
  });

  it('shows a generic error message when loading fails', () => {
    const { fixture } = setup({ error: 'dashboard.errors.generic', metrics: null as unknown as ReturnType<typeof metricsFixture> });

    expect(fixture.nativeElement.textContent).toContain('dashboard.errors.generic');
  });

  it('opens the AI assistant from the assistant preview card', () => {
    const { fixture, assistantStateMock } = setup();

    const button = fixture.nativeElement.querySelector(
      '[data-testid="dashboard-assistant-preview"] button',
    ) as HTMLButtonElement;
    button.click();

    expect(assistantStateMock.open).toHaveBeenCalled();
  });
});
