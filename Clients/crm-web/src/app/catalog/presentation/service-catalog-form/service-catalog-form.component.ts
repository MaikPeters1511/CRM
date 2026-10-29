import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ServiceCatalogStateService } from '../../application/service-catalog-state.service';
import { ServiceFormValue } from '../../domain/service.model';

/**
 * Smart/Container-Component: Leistung anlegen oder bearbeiten.
 * Card/Fieldset-Layout analog `CustomerFormComponent`.
 */
@Component({
  selector: 'app-service-catalog-form',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './service-catalog-form.component.html',
})
export class ServiceCatalogFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly state = inject(ServiceCatalogStateService);

  private readonly _serviceId = signal<string | null>(null);
  protected readonly serviceId = this._serviceId.asReadonly();
  protected readonly isEditMode = computed(() => this.serviceId() !== null);

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required]],
    description: [''],
    defaultDurationMinutes: this.fb.control<number | null>(null),
  });

  /** Befuellt das Formular reaktiv, sobald die Leistung asynchron geladen wurde. */
  private readonly patchFormOnLoadEffect = effect(() => {
    const service = this.state.selectedService();
    if (!service || service.id !== this.serviceId()) {
      return;
    }
    this.form.patchValue({
      name: service.name,
      description: service.description ?? '',
      defaultDurationMinutes: service.defaultDurationMinutes,
    });
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this._serviceId.set(id);
      this.state.loadServiceById(id);
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const value: ServiceFormValue = {
      name: raw.name,
      description: raw.description || null,
      defaultDurationMinutes: raw.defaultDurationMinutes,
    };

    const id = this.serviceId();
    if (id) {
      this.state.updateService(id, value, () => this.router.navigate(['/catalog']));
    } else {
      this.state.createService(value, () => this.router.navigate(['/catalog']));
    }
  }

  cancel(): void {
    this.router.navigate(['/catalog']);
  }
}
