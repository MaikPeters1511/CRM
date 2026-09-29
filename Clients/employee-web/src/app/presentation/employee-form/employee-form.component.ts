import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { EmployeeStateService } from '../../application/employee-state.service';
import { EmployeeServiceAssignment, EmployeeRole } from '../../domain/employee.model';

/**
 * Smart/Container-Component: Mitarbeiter anlegen oder bearbeiten
 * (Stammdaten, Rolle, Leistungs-Zuordnung).
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
  protected readonly state = inject(EmployeeStateService);

  private readonly _employeeId = signal<string | null>(null);
  protected readonly employeeId = this._employeeId.asReadonly();
  protected readonly isEditMode = computed(() => this.employeeId() !== null);

  private readonly _services = signal<EmployeeServiceAssignment[]>([]);
  protected readonly services = this._services.asReadonly();

  protected readonly form = this.fb.nonNullable.group({
    firstName: ['', [Validators.required]],
    lastName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    role: ['Employee' as EmployeeRole, [Validators.required]],
  });

  protected readonly newServiceForm = this.fb.nonNullable.group({
    serviceId: [''],
    serviceName: [''],
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
  }

  addService(): void {
    const raw = this.newServiceForm.getRawValue();
    if (!raw.serviceId.trim() || !raw.serviceName.trim()) {
      return;
    }
    this._services.update((list) => [...list, { serviceId: raw.serviceId.trim(), serviceName: raw.serviceName.trim() }]);
    this.newServiceForm.reset({ serviceId: '', serviceName: '' });
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
