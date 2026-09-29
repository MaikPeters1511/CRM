import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { CustomerStateService } from '../../application/customer-state.service';
import { CustomerFormValue } from '../../domain/customer.model';

/**
 * Validator: E-Mail ODER Telefonnummer muss angegeben werden (Pflichtfeld-Kombination).
 */
function emailOrPhoneRequired(control: AbstractControl): ValidationErrors | null {
  const email = control.get('email')?.value?.trim();
  const phone = control.get('phone')?.value?.trim();
  return email || phone ? null : { contactRequired: true };
}

/**
 * Smart/Container-Component: Kunde anlegen oder bearbeiten.
 */
@Component({
  selector: 'app-customer-form',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './customer-form.component.html',
})
export class CustomerFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly state = inject(CustomerStateService);

  private readonly _customerId = signal<string | null>(null);
  protected readonly customerId = this._customerId.asReadonly();
  protected readonly isEditMode = computed(() => this.customerId() !== null);

  protected readonly form = this.fb.nonNullable.group(
    {
      firstName: ['', [Validators.required]],
      lastName: ['', [Validators.required]],
      email: [''],
      phone: [''],
      street: [''],
      city: [''],
      zipCode: [''],
      country: [''],
    },
    { validators: emailOrPhoneRequired },
  );

  /** Befuellt das Formular reaktiv, sobald der Kunde asynchron geladen wurde. */
  private readonly patchFormOnLoadEffect = effect(() => {
    const customer = this.state.selectedCustomer();
    if (!customer || customer.id !== this.customerId()) {
      return;
    }
    this.form.patchValue({
      firstName: customer.firstName,
      lastName: customer.lastName,
      email: customer.email ?? '',
      phone: customer.phone ?? '',
      street: customer.address?.street ?? '',
      city: customer.address?.city ?? '',
      zipCode: customer.address?.zipCode ?? '',
      country: customer.address?.country ?? '',
    });
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this._customerId.set(id);
      this.state.loadCustomerById(id);
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const hasAddress = !!(raw.street || raw.city || raw.zipCode || raw.country);
    const value: CustomerFormValue = {
      firstName: raw.firstName,
      lastName: raw.lastName,
      email: raw.email || null,
      phone: raw.phone || null,
      address: hasAddress
        ? { street: raw.street, city: raw.city, zipCode: raw.zipCode, country: raw.country }
        : null,
    };

    const id = this.customerId();
    if (id) {
      this.state.updateCustomer(id, value, (customer) => this.router.navigate(['/customers', customer.id]));
    } else {
      this.state.createCustomer(value, (customer) => this.router.navigate(['/customers', customer.id]));
    }
  }

  cancel(): void {
    this.router.navigate(['/customers']);
  }
}
