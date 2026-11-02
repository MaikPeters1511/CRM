import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, finalize, forkJoin, of, tap } from 'rxjs';
import { Notification } from '../domain/notification.model';
import { NotificationApiService } from '../infrastructure/notification-api.service';
import { isBackendUnavailable } from '../infrastructure/api-error.util';

/**
 * Application-Layer: State der Benachrichtigungs-Glocke (ungelesen-Zaehler, Liste,
 * "Alle als gelesen markieren"). Zeigt einen sauberen Leerzustand statt abzustuerzen,
 * solange die Notifications-API (paralleler Branch `abwesenheit-workflow`) noch nicht
 * existiert.
 */
@Injectable({ providedIn: 'root' })
export class NotificationState {
  private readonly api = inject(NotificationApiService);

  private readonly _notifications = signal<Notification[]>([]);
  private readonly _loading = signal(false);
  private readonly _backendUnavailable = signal(false);

  readonly notifications = this._notifications.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly backendUnavailable = this._backendUnavailable.asReadonly();
  readonly unreadCount = computed(() => this._notifications().filter((n) => !n.read).length);

  load(): void {
    this._loading.set(true);
    this._backendUnavailable.set(false);
    this.api
      .getAll()
      .pipe(
        tap((notifications) => this._notifications.set(notifications)),
        catchError((error: unknown) => {
          this._notifications.set([]);
          if (isBackendUnavailable(error)) {
            this._backendUnavailable.set(true);
          }
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  markAsRead(id: string): void {
    const previous = this._notifications();
    this._notifications.update((list) => list.map((n) => (n.id === id ? { ...n, read: true } : n)));

    this.api
      .markAsRead(id)
      .pipe(
        catchError(() => {
          this._notifications.set(previous);
          return of(null);
        }),
      )
      .subscribe();
  }

  markAllAsRead(): void {
    const unread = this._notifications().filter((n) => !n.read);
    if (unread.length === 0) {
      return;
    }
    const previous = this._notifications();
    this._notifications.update((list) => list.map((n) => ({ ...n, read: true })));

    forkJoin(unread.map((n) => this.api.markAsRead(n.id)))
      .pipe(
        catchError(() => {
          this._notifications.set(previous);
          return of(null);
        }),
      )
      .subscribe();
  }
}
