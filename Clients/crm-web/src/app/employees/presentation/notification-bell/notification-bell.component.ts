import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { timer } from 'rxjs';
import { NotificationStateService } from '../../application/notification-state.service';
import { relativeTimeFrom } from '../../domain/notification-relative-time.util';
import { Notification } from '../../domain/notification.model';

/**
 * Glocken-Komponente der App-Shell (abwesenheit-workflow.openspec.md Abschnitt 4):
 * Button mit ungelesen-Zaehler-Badge (`aria-live="polite"`), Dropdown mit Liste
 * (Text, relative Zeit, Link). Klick markiert als gelesen und navigiert zum Link.
 *
 * Design-Entscheidung Polling vs. Lazy-Load (siehe Aufgabenstellung): Hybrid-Ansatz.
 * `loadNotifications()` laeuft bereits beim Initialisieren und danach alle 30 Sekunden
 * im Hintergrund (`timer(0, 30000)`), damit der ungelesen-Zaehler auch dann aktuell
 * bleibt, wenn der Nutzer das Dropdown nie oeffnet (reines Lazy-Load wuerde den Zaehler
 * sonst beliebig lange veraltern lassen). Zusaetzlich wird beim Oeffnen des Dropdowns
 * (`toggle()`) sofort erneut geladen, damit die sichtbare Liste moeglichst aktuell ist,
 * ohne auf das naechste 30s-Intervall warten zu muessen. 30 Sekunden sind ein bewusster
 * Mittelweg zwischen Aktualitaet und Backend-Last fuer einen rein informativen Zaehler.
 */
@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './notification-bell.component.html',
})
export class NotificationBellComponent implements OnInit {
  private readonly state = inject(NotificationStateService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly notifications = this.state.notifications;
  protected readonly unreadCount = this.state.unreadCount;
  protected readonly loading = this.state.loading;

  private readonly _open = signal(false);
  readonly open = this._open.asReadonly();

  ngOnInit(): void {
    this.state.loadNotifications();
    timer(30000, 30000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.state.loadNotifications());
  }

  toggle(): void {
    const willOpen = !this._open();
    this._open.set(willOpen);
    if (willOpen) {
      this.state.loadNotifications();
    }
  }

  close(): void {
    this._open.set(false);
  }

  select(notification: Notification): void {
    this.state.markAsRead(notification.id);
    this.close();
    if (notification.link) {
      void this.router.navigateByUrl(notification.link);
    }
  }

  relativeTimeKey(notification: Notification): string {
    return `notification.time.${relativeTimeFrom(notification.createdAt).unit}`;
  }

  relativeTimeValue(notification: Notification): { value: number } {
    return { value: relativeTimeFrom(notification.createdAt).value };
  }
}
