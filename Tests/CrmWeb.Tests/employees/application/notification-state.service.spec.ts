import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { NotificationStateService } from '../../../../Clients/crm-web/src/app/employees/application/notification-state.service';
import { NotificationApiService } from '../../../../Clients/crm-web/src/app/employees/infrastructure/notification-api.service';
import { Notification } from '../../../../Clients/crm-web/src/app/employees/domain/notification.model';

/**
 * Deckt das Requirement "Benachrichtigung bei Entscheidung ueber Abwesenheitsanfrage" ab
 * (abwesenheit-workflow.openspec.md Abschnitt 3, Glocken-Komponente).
 */
describe('NotificationStateService', () => {
  const unread: Notification = {
    id: 'n1',
    recipientEmployeeId: 'e1',
    text: 'Ihr Urlaubsantrag wurde genehmigt.',
    link: '/employees/e1/absences/new',
    isRead: false,
    createdAt: '2026-10-20T10:00:00Z',
  };
  const read: Notification = { ...unread, id: 'n2', isRead: true };

  function createApiMock() {
    return {
      getNotifications: vi.fn().mockReturnValue(of([unread, read])),
      markAsRead: vi.fn().mockReturnValue(of(undefined)),
    };
  }

  function setup(apiOverrides: Partial<ReturnType<typeof createApiMock>> = {}) {
    const apiMock = { ...createApiMock(), ...apiOverrides };
    TestBed.configureTestingModule({
      providers: [NotificationStateService, { provide: NotificationApiService, useValue: apiMock }],
    });
    return { service: TestBed.inject(NotificationStateService), apiMock };
  }

  it('loads the own notifications (Scenario: Benachrichtigung nach Genehmigung)', () => {
    const { service, apiMock } = setup();

    service.loadNotifications();

    expect(apiMock.getNotifications).toHaveBeenCalled();
    expect(service.notifications()).toEqual([unread, read]);
  });

  it('computes the unread count from the loaded notifications', () => {
    const { service } = setup();

    service.loadNotifications();

    expect(service.unreadCount()).toBe(1);
  });

  it('marks a notification as read and decrements the unread count (Scenario: Benachrichtigung als gelesen markieren)', () => {
    const { service, apiMock } = setup();
    service.loadNotifications();

    service.markAsRead('n1');

    expect(apiMock.markAsRead).toHaveBeenCalledWith('n1');
    expect(service.unreadCount()).toBe(0);
    expect(service.notifications().find((n) => n.id === 'n1')?.isRead).toBe(true);
  });

  it('rolls back the optimistic read-marking when the backend call fails', () => {
    const { service } = setup({ markAsRead: vi.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 }))) });
    service.loadNotifications();

    service.markAsRead('n1');

    expect(service.unreadCount()).toBe(1);
    expect(service.notifications().find((n) => n.id === 'n1')?.isRead).toBe(false);
  });
});
