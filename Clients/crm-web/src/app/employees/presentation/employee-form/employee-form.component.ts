import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { tap } from 'rxjs';
import { EmployeeStateService } from '../../application/employee-state.service';
import { EmployeeServiceAssignment, EmployeeRole } from '../../domain/employee.model';
import { ActiveService } from '../../domain/active-service.model';
import { ServiceLookupApiService } from '../../infrastructure/service-lookup-api.service';

/**
 * Smart/Container-Component: Mitarbeiter anlegen oder bearbeiten
 * (Stammdaten, Rolle, Leistungs-Zuordnung aus dem zentralen Leistungskatalog).
 */
@Component({
  selector: 'app-employee-form',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './employee-form.component.html',
})
export class EmployeeFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly serviceLookup = inject(ServiceLookupApiService);
  protected readonly state = inject(EmployeeStateService);

  private readonly _employeeId = signal<string | null>(null);
  protected readonly employeeId = this._employeeId.asReadonly();
  protected readonly isEditMode = computed(() => this.employeeId() !== null);

  private readonly _services = signal<EmployeeServiceAssignment[]>([]);
  protected readonly services = this._services.asReadonly();

  /** Aktive Leistungen aus dem Katalog (`GET /api/services?status=true`) fuer die Auswahl. */
  private readonly _availableServices = signal<ActiveService[]>([]);
  protected readonly availableServices = this._availableServices.asReadonly();

  /**
   * Fuer die Auswahlliste bleiben bereits zugeordnete Leistungen ausgeblendet - verhindert
   * Doppelzuordnung. Bereits zugeordnete, inzwischen im Katalog deaktivierte Leistungen bleiben
   * unabhaengig davon in `services()` sichtbar (Snapshot-Verhalten), da sie dort nie entfernt werden.
   */
  protected readonly selectableServices = computed(() => {
    const assignedIds = new Set(this._services().map((s) => s.serviceId));
    return this._availableServices().filter((service) => !assignedIds.has(service.id));
  });

  protected readonly form = this.fb.nonNullable.group({
    firstName: ['', [Validators.required]],
    lastName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    role: ['Employee' as EmployeeRole, [Validators.required]],
  });

  protected readonly newServiceForm = this.fb.nonNullable.group({
    selectedServiceId: [''],
  });

  /** Befuellt das Formular reaktiv, sobald der Mitarbeiter asynchron geladen wurde. */
  private readonly patchFormOnLoadEffect = effect(() => {
    const employee = this.state.selectedEmployee();
    if (!employee || employee.id !== this.employeeId()) {
      return;
    }
    this.form.patchValue({
      firstName: employee.firstName,
      lastName: employee.lastName,
      email: employee.email,
      role: employee.role,
    });
    this._services.set(employee.services);
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this._employeeId.set(id);
      this.state.loadEmployeeById(id);
    }
    this.serviceLookup
      .getActiveServices()
      .pipe(tap((services) => this._availableServices.set(services)))
      .subscribe();
  }

  addService(): void {
    const selectedServiceId = this.newServiceForm.getRawValue().selectedServiceId;
    if (!selectedServiceId) {
      return;
    }
    const service = this._availableServices().find((s) => s.id === selectedServiceId);
    if (!service) {
      return;
    }
    this._services.update((list) => [...list, { serviceId: service.id, serviceName: service.name }]);
    this.newServiceForm.reset({ selectedServiceId: '' });
  }

  removeService(serviceId: string): void {
    this._services.update((list) => list.filter((s) => s.serviceId !== serviceId));
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const id = this.employeeId();
    if (id) {
      this.state.updateEmployee(
        id,
        { ...raw, services: this._services() },
        (employee) => this.router.navigate(['/employees', employee.id, 'edit']),
      );
    } else {
      this.state.createEmployee(raw, (employee) => this.router.navigate(['/employees', employee.id, 'edit']));
    }
  }

  cancel(): void {
    this.router.navigate(['/employees']);
  }
}
