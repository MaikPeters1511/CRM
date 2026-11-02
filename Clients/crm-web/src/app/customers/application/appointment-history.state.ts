import { Injectable, inject, signal } from '@angular/core';
import { catchError, finalize, of, tap } from 'rxjs';
import { AppointmentHistoryEntry } from '../domain/appointment-history.model';
import { AppointmentHistoryApiService } from '../infrastructure/appointment-history-api.service';

const HISTORY_FROM = '2000-01-01';
const HISTORY_TO = '2100-01-01';

/**
 * Application-Layer: vollstaendige Terminhistorie eines Kunden (echte Daten gegen
 * `appointment-service`, siehe `AppointmentHistoryApiService`). Wird in der
 * Kundendetailsicht angezeigt.
 */
@Injectable({ providedIn: 'root' })
export class AppointmentHistoryState {
  private readonly api = inject(AppointmentHistoryApiService);

  private readonly _entries = signal<AppointmentHistoryEntry[]>([]);
  private readonly _loading = signal(false);
  private readonly _loadFailed = signal(false);

  readonly entries = this._entries.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly loadFailed = this._loadFailed.asReadonly();

  loadForCustomer(customerId: string): void {
    this._loading.set(true);
    this._loadFailed.set(false);
    this.api
      .getForCustomer(customerId, HISTORY_FROM, HISTORY_TO)
      .pipe(
        tap((entries) => {
          const sorted = [...entries].sort((a, b) => b.date.localeCompare(a.date) || b.startTime.localeCompare(a.startTime));
          this._entries.set(sorted);
        }),
        catchError(() => {
          this._entries.set([]);
          this._loadFailed.set(true);
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }
}
