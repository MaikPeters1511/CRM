import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { timer } from 'rxjs';
import { LocalSettingsState } from '../../application/local-settings.state';

/**
 * Smart/Container-Component: Kalender-Sync-Integration. Kein Backend vorhanden (siehe
 * Scope-Abgrenzung) - rein lokaler UI-Zustand ohne Persistenz-Anspruch.
 */
@Component({
  selector: 'app-calendar-sync-settings',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './calendar-sync-settings.component.html',
})
export class CalendarSyncSettingsComponent {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly state = inject(LocalSettingsState);

  private readonly current = this.state.calendarSync();

  protected readonly form = this.fb.nonNullable.group({
    provider: [this.current.provider],
    syncEnabled: [this.current.syncEnabled],
    calendarId: [this.current.calendarId],
  });

  private readonly _savedVisible = signal(false);
  protected readonly savedVisible = this._savedVisible.asReadonly();

  save(): void {
    this.state.saveCalendarSync(this.form.getRawValue());
    this._savedVisible.set(true);
    timer(2500)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this._savedVisible.set(false));
  }
}
