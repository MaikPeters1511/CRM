import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { timer } from 'rxjs';
import { LocalSettingsState } from '../../application/local-settings.state';

/**
 * Smart/Container-Component: SMTP-Integration. Kein Backend vorhanden (siehe
 * Scope-Abgrenzung) - rein lokaler UI-Zustand ohne Persistenz-Anspruch. Das Passwort selbst
 * wird bewusst nicht erfasst (kein Secrets-Management im Frontend, siehe CLAUDE.md).
 */
@Component({
  selector: 'app-smtp-settings',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './smtp-settings.component.html',
})
export class SmtpSettingsComponent {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly state = inject(LocalSettingsState);

  private readonly current = this.state.smtp();

  protected readonly form = this.fb.nonNullable.group({
    host: [this.current.host],
    port: [this.current.port],
    username: [this.current.username],
    useSsl: [this.current.useSsl],
    senderAddress: [this.current.senderAddress],
  });

  private readonly _savedVisible = signal(false);
  protected readonly savedVisible = this._savedVisible.asReadonly();

  save(): void {
    this.state.saveSmtp(this.form.getRawValue());
    this._savedVisible.set(true);
    timer(2500)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this._savedVisible.set(false));
  }
}
