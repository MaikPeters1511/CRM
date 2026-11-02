import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { NotificationBellComponent } from '../../../../Clients/crm-web/src/app/employees/presentation/notification-bell/notification-bell.component';
import { NotificationStateService } from '../../../../Clients/crm-web/src/app/employees/application/notification-state.service';
import { Notification } from '../../../../Clients/crm-web/src/app/employees/domain/notification.model';

/**
 * Deckt die UI-Requirements der Glocken-Komponente ab (abwesenheit-workflow.openspec.md
 * Abschnitt 4): ungelesen-Zaehler-Badge, Dropdown mit Liste, "als gelesen markieren" +
 * Navigation zum Link.
 */
describe('NotificationBellComponent', () => {
  const notification: Notification = {
    id: 'n1',
    recipientEmployeeId: 'e1',
    text: 'Ihr Urlaubsantrag wurde genehmigt.',
    link: '/employees/e1/absences/new',
    isRead: false,
    createdAt: new Date().toISOString(),
  };

  function setup(stateOverrides: Record<string, unknown> = {}) {
    const stateMock = {
      notifications: () => [notification],
      unreadCount: () => 1,
      loading: () => false,
      loadNotifications: vi.fn(),
      markAsRead: vi.fn(),
      ...stateOverrides,
    };

    TestBed.configureTestingModule({
      imports: [NotificationBellComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: NotificationStateService, useValue: stateMock },
      ],
    });

    const fixture = TestBed.createComponent(NotificationBellComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const routerMock = { navigateByUrl: vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true) };
    return { fixture, stateMock, routerMock, component: fixture.componentInstance };
  }

  it('loads notifications on init so the unread badge is accurate without opening the dropdown', () => {
    const { stateMock } = setup();

    expect(stateMock.loadNotifications).toHaveBeenCalled();
  });

  it('renders the unread count as an aria-live badge', () => {
    const { fixture } = setup();

    const badge = fixture.nativeElement.querySelector('[aria-live="polite"]');
    expect(badge).toBeTruthy();
    expect(badge.textContent).toContain('1');
  });

  it('exposes the bell button with aria-haspopup and an aria-label', () => {
    const { fixture } = setup();

    const button = fixture.nativeElement.querySelector('button[aria-haspopup]');
    expect(button).toBeTruthy();
    expect(button.getAttribute('aria-label')).toBeTruthy();
  });

  it('does not render the dropdown list before the bell is opened', () => {
    const { fixture } = setup();

    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeFalsy();
  });

  it('opens the dropdown and reloads notifications on toggle', () => {
    const { fixture, stateMock, component } = setup();
    stateMock.loadNotifications.mockClear();

    component.toggle();
    fixture.detectChanges();

    expect(stateMock.loadNotifications).toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeTruthy();
  });

  it('marks a notification as read and navigates to its link on selection', () => {
    const { routerMock, stateMock, component } = setup();

    component.select(notification);

    expect(stateMock.markAsRead).toHaveBeenCalledWith('n1');
    expect(routerMock.navigateByUrl).toHaveBeenCalledWith('/employees/e1/absences/new');
  });

  it('does not navigate when the notification has no link', () => {
    const { routerMock, component } = setup();

    component.select({ ...notification, link: null });

    expect(routerMock.navigateByUrl).not.toHaveBeenCalled();
  });

  it('shows the empty state when there are no notifications', () => {
    const { fixture, component } = setup({ notifications: () => [], unreadCount: () => 0 });
    component.toggle();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('notification.bell.empty');
  });
});
