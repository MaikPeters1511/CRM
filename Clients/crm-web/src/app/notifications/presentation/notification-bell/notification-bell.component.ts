import { Component, OnInit, inject, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { NotificationState } from '../../application/notification.state';

/**
 * Smart/Container-Component: Benachrichtigungs-Glocke in der Sidebar mit Dropdown
 * (ungelesen-Zaehler, Liste, "Alle als gelesen markieren"). Zeigt einen sauberen
 * Leerzustand statt abzustuerzen, solange die Notifications-API noch nicht existiert.
 */
@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './notification-bell.component.html',
})
export class NotificationBellComponent implements OnInit {
  protected readonly state = inject(NotificationState);

  private readonly _open = signal(false);
  protected readonly open = this._open.asReadonly();

  ngOnInit(): void {
    this.state.load();
  }

  toggle(): void {
    this._open.update((value) => !value);
  }

  close(): void {
    this._open.set(false);
  }

  markAsRead(id: string): void {
    this.state.markAsRead(id);
  }

  markAllAsRead(): void {
    this.state.markAllAsRead();
  }
}
