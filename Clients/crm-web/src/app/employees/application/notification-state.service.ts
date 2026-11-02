import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, of, tap } from 'rxjs';
import { Notification } from '../domain/notification.model';
import { NotificationApiService } from '../infrastructure/notification-api.service';

/**
 * Application-Layer: zentraler State fuer Benachrichtigungen (Glocken-Komponente in der
 * App-Shell, abwesenheit-workflow.openspec.md Abschnitt 4). Haelt die eigenen
 * Benachrichtigungen und den ungelesen-Zaehler als Signal; markiert optimistisch als
 * gelesen mit Rollback bei Fehlschlag.
 */
@Injectable({ providedIn: 'root' })
export class NotificationStateService {
  private readonly api = inject(NotificationApiService);

  private readonly _notifications = signal<Notification[]>([]);
  private readonly _loading = signal(false);
  private readonly _errors = signal<string[]>([]);

  readonly notifications = this._notifications.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly errors = this._errors.asReadonly();
  readonly unreadCount = computed(() => this._notifications().filter((n) => !n.isRead).length);

  loadNotifications(): void {
    this._loading.set(true);
    this._errors.set([]);
    this.api
      .getNotifications()
      .pipe(
        tap((notifications) => this._notifications.set(notifications)),
        catchError(() => {
          this._errors.set(['notification.bell.errors.loadFailed']);
          return of(null);
        }),
      )
      .subscribe(() => this._loading.set(false));
  }

  /** Markiert eine Benachrichtigung optimistisch als gelesen; Rollback bei Fehlschlag. */
  markAsRead(id: string): void {
    const previous = this._notifications();
    const alreadyRead = previous.find((n) => n.id === id)?.isRead ?? true;
    if (alreadyRead) {
      return;
    }

    this._notifications.update((list) => list.map((n) => (n.id === id ? { ...n, isRead: true } : n)));

    this.api
      .markAsRead(id)
      .pipe(
        catchError(() => {
          this._notifications.set(previous);
          this._errors.set(['notification.bell.errors.markReadFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }
}
