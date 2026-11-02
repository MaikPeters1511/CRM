import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { timer } from 'rxjs';
import { LocalSettingsState } from '../../application/local-settings.state';

/**
 * Smart/Container-Component: Zahlungsanbieter-Integration (Stripe). Kein Backend vorhanden
 * (siehe Scope-Abgrenzung) - rein lokaler UI-Zustand ohne Persistenz-Anspruch. Der Secret Key
 * selbst wird NIE erfasst (nur ein Flag "ist konfiguriert"), um keine Zahlungs-Credentials im
 * Klartext im `localStorage` zu halten.
 */
@Component({
  selector: 'app-payment-provider-settings',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './payment-provider-settings.component.html',
})
export class PaymentProviderSettingsComponent {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly state = inject(LocalSettingsState);

  private readonly current = this.state.payment();

  protected readonly form = this.fb.nonNullable.group({
    provider: [this.current.provider],
    publishableKey: [this.current.publishableKey],
    secretKeyConfigured: [this.current.secretKeyConfigured],
  });

  private readonly _savedVisible = signal(false);
  protected readonly savedVisible = this._savedVisible.asReadonly();

  save(): void {
    this.state.savePayment(this.form.getRawValue());
    this._savedVisible.set(true);
    timer(2500)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this._savedVisible.set(false));
  }
}
