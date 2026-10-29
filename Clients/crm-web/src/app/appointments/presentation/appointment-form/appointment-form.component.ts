import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged, skip, switchMap } from 'rxjs';
import { CalendarStateService } from '../../application/calendar-state.service';
import { CustomerApiService } from '../../infrastructure/customer-api.service';
import { EmployeeApiService } from '../../infrastructure/employee-api.service';
import { CustomerLookupItem } from '../../domain/customer.model';
import { AvailableSlot, EmployeeLookupItem, EmployeeServiceAssignment } from '../../domain/employee.model';

/**
 * Smart/Container-Component: Terminformular (terminverwaltung.openspec.md Abschnitt 4).
 * Kunde-Autocomplete gegen `customer-service`, Mitarbeiter-/Leistungsauswahl sowie
 * Zeitfenster-Picker mit Live-Verfuegbarkeitsanzeige gegen `employee-service`.
 */
@Component({
  selector: 'app-appointment-form',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './appointment-form.component.html',
})
export class AppointmentFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly customerApi = inject(CustomerApiService);
  private readonly employeeApi = inject(EmployeeApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = inject(CalendarStateService);

  readonly form = this.fb.nonNullable.group({
    customerId: ['', Validators.required],
    employeeId: ['', Validators.required],
    serviceId: ['', Validators.required],
    date: ['', Validators.required],
    startTime: ['', Validators.required],
    endTime: ['', Validators.required],
  });

  private readonly _customerQuery = signal('');
  readonly customerQuery = this._customerQuery.asReadonly();
  private readonly _customerResults = signal<CustomerLookupItem[]>([]);
  readonly customerResults = this._customerResults.asReadonly();
  private readonly _selectedCustomerLabel = signal<string | null>(null);
  readonly selectedCustomerLabel = this._selectedCustomerLabel.asReadonly();

  private readonly _employeeQuery = signal('');
  readonly employeeQuery = this._employeeQuery.asReadonly();
  private readonly _employees = signal<EmployeeLookupItem[]>([]);
  readonly employees = this._employees.asReadonly();
  private readonly _selectedEmployeeId = signal('');
  readonly selectedEmployeeServices = computed<EmployeeServiceAssignment[]>(() => {
    const employeeId = this._selectedEmployeeId();
    return this._employees().find((e) => e.id === employeeId)?.services ?? [];
  });

  private readonly _availableSlots = signal<AvailableSlot[]>([]);
  readonly availableSlots = this._availableSlots.asReadonly();

  constructor() {
    toObservable(this._customerQuery)
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) =>
          term.length >= 2
            ? this.customerApi.search({ search: term, status: true, page: 1 })
            : [{ items: [], totalCount: 0, page: 1, pageSize: 0 }],
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => this._customerResults.set(result.items));

    // Sinnvolle Default-Liste beim initialen Laden (kein Suchbegriff) - damit das Select nicht
    // leer startet, bevor der Nutzer ueberhaupt etwas in das Suchfeld eingegeben hat.
    this.employeeApi.search({ search: null, page: 1 }).subscribe({
      next: (result) => this._employees.set(result.items.filter((e) => e.isActive)),
      error: () => this._employees.set([]),
    });

    // Echte, debounced serverseitige Suche (Bug 10 Nachfolge-Fix): `skip(1)` ignoriert die
    // synchrone Erstemission von `toObservable` fuer den initialen (leeren) Suchbegriff, die
    // bereits ueber den obigen Default-Aufruf abgedeckt ist.
    toObservable(this._employeeQuery)
      .pipe(
        skip(1),
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => this.employeeApi.search({ search: term.trim() || null, page: 1 })),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (result) => this._employees.set(result.items.filter((e) => e.isActive)),
        error: () => this._employees.set([]),
      });

    this.form.controls.employeeId.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((employeeId) => this._selectedEmployeeId.set(employeeId));
  }

  onCustomerQueryInput(value: string): void {
    this._customerQuery.set(value);
    this._selectedCustomerLabel.set(null);
    this.form.controls.customerId.setValue('');
  }

  selectCustomer(customer: CustomerLookupItem): void {
    this.form.controls.customerId.setValue(customer.id);
    this._selectedCustomerLabel.set(`${customer.firstName} ${customer.lastName}`);
    this._customerResults.set([]);
  }

  onEmployeeQueryInput(value: string): void {
    this._employeeQuery.set(value);
  }

  onEmployeeChange(employeeId: string): void {
    this.form.controls.employeeId.setValue(employeeId);
    this.form.controls.serviceId.setValue('');
    this.loadAvailability();
  }

  onDateChange(date: string): void {
    this.form.controls.date.setValue(date);
    this.loadAvailability();
  }

  private loadAvailability(): void {
    const employeeId = this.form.controls.employeeId.value;
    const date = this.form.controls.date.value;
    if (!employeeId || !date) {
      this._availableSlots.set([]);
      return;
    }
    this.employeeApi.getAvailability(employeeId, date, date).subscribe({
      next: (slots) => this._availableSlots.set(slots),
      error: () => this._availableSlots.set([]),
    });
  }

  applySlot(slot: AvailableSlot): void {
    this.form.controls.startTime.setValue(slot.startTime.slice(0, 5));
    this.form.controls.endTime.setValue(slot.endTime.slice(0, 5));
  }

  submit(): void {
    this.state.clearErrors();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const start = `${value.date}T${value.startTime}:00`;
    const end = `${value.date}T${value.endTime}:00`;

    this.state.createAppointment(
      {
        customerId: value.customerId,
        employeeId: value.employeeId,
        serviceId: value.serviceId,
        start,
        end,
      },
      () => void this.router.navigateByUrl('/appointments'),
    );
  }

  cancel(): void {
    void this.router.navigateByUrl('/appointments');
  }
}
