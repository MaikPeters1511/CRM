import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { timer } from 'rxjs';
import { LocalSettingsState } from '../../application/local-settings.state';

/**
 * Smart/Container-Component: DATEV-Export-Konfiguration. Kein Backend vorhanden (siehe
 * Scope-Abgrenzung) - rein lokaler UI-Zustand ohne Persistenz-Anspruch; es findet aktuell
 * kein echter Export statt.
 */
@Component({
  selector: 'app-datev-export-settings',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './datev-export-settings.component.html',
})
export class DatevExportSettingsComponent {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly state = inject(LocalSettingsState);

  private readonly current = this.state.datev();

  protected readonly form = this.fb.nonNullable.group({
    consultantNumber: [this.current.consultantNumber],
    clientNumber: [this.current.clientNumber],
    exportFormat: [this.current.exportFormat],
  });

  private readonly _savedVisible = signal(false);
  protected readonly savedVisible = this._savedVisible.asReadonly();

  save(): void {
    this.state.saveDatev(this.form.getRawValue());
    this._savedVisible.set(true);
    timer(2500)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this._savedVisible.set(false));
  }
}
