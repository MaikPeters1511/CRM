import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { timer } from 'rxjs';
import { LocalSettingsState } from '../../application/local-settings.state';

/**
 * Smart/Container-Component: Firmendaten/Logo/Oeffnungszeiten. Rein lokaler UI-Zustand ohne
 * Backend-Anbindung (siehe `LocalSettingsState`), Persistenz im `localStorage`.
 */
@Component({
  selector: 'app-general-settings',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, TranslatePipe],
  templateUrl: './general-settings.component.html',
})
export class GeneralSettingsComponent {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly state = inject(LocalSettingsState);

  private readonly current = this.state.company();

  protected readonly form = this.fb.nonNullable.group({
    companyName: [this.current.companyName, Validators.required],
    logoUrl: [this.current.logoUrl ?? ''],
    street: [this.current.street],
    zipCode: [this.current.zipCode],
    city: [this.current.city],
    phone: [this.current.phone],
    email: [this.current.email],
  });

  protected readonly openingHours = signal(this.current.openingHours);

  private readonly _savedVisible = signal(false);
  protected readonly savedVisible = this._savedVisible.asReadonly();

  updateOpeningHoursField(index: number, field: 'from' | 'to' | 'closed', value: string | boolean): void {
    this.openingHours.update((slots) =>
      slots.map((slot, i) => (i === index ? { ...slot, [field]: value } : slot)),
    );
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.state.saveCompany({
      companyName: value.companyName,
      logoUrl: value.logoUrl || null,
      street: value.street,
      zipCode: value.zipCode,
      city: value.city,
      phone: value.phone,
      email: value.email,
      openingHours: this.openingHours(),
    });
    this._savedVisible.set(true);
    timer(2500)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this._savedVisible.set(false));
  }
}
