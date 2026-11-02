import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { timer } from 'rxjs';
import { LocalSettingsState } from '../../application/local-settings.state';

/**
 * Smart/Container-Component: Telefonie-Integration (3CX). Es gibt aktuell GAR KEIN Backend
 * fuer eine echte 3CX-Anbindung - dieses Formular bleibt bewusst rein lokaler
 * UI-Zustand ohne Persistenz-Anspruch jenseits von `localStorage` (siehe Scope-Abgrenzung).
 */
@Component({
  selector: 'app-telephony-settings',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './telephony-settings.component.html',
})
export class TelephonySettingsComponent {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly state = inject(LocalSettingsState);

  private readonly current = this.state.telephony();

  protected readonly form = this.fb.nonNullable.group({
    enabled: [this.current.enabled],
    pbxHost: [this.current.pbxHost],
    extension: [this.current.extension],
    apiKey: [this.current.apiKey],
  });

  private readonly _savedVisible = signal(false);
  protected readonly savedVisible = this._savedVisible.asReadonly();

  save(): void {
    this.state.saveTelephony(this.form.getRawValue());
    this._savedVisible.set(true);
    timer(2500)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this._savedVisible.set(false));
  }
}
