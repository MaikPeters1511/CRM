import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { EmployeeStateService } from '../../application/employee-state.service';
import { AbsenceType } from '../../domain/employee.model';

/**
 * Smart/Container-Component: Abwesenheit (Urlaub/Krankheit/Sonstige) anlegen.
 * Bildet die Konfliktwarnung des Backends ab (Terminkollision) und erfordert
 * bei Konflikt eine explizite Override-Bestaetigung (`confirmOverride`).
 */
@Component({
  selector: 'app-absence-form',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './absence-form.component.html',
})
export class AbsenceFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly state = inject(EmployeeStateService);

  private employeeId = '';

  protected readonly form = this.fb.nonNullable.group({
    type: ['Vacation' as AbsenceType, [Validators.required]],
    from: ['', [Validators.required]],
    to: ['', [Validators.required]],
    confirmOverride: [false],
  });

  ngOnInit(): void {
    this.employeeId = this.route.snapshot.paramMap.get('id') ?? '';
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    this.state.addAbsence(
      this.employeeId,
      { type: raw.type, from: raw.from, to: raw.to, confirmOverride: raw.confirmOverride },
      () => this.router.navigate(['/employees']),
    );
  }

  /** Bestaetigt die Abwesenheit trotz gemeldetem Terminkonflikt erneut mit `confirmOverride = true`. */
  confirmOverrideAndRetry(): void {
    this.form.patchValue({ confirmOverride: true });
    this.save();
  }

  cancelConflict(): void {
    this.state.clearAbsenceConflict();
  }

  cancel(): void {
    this.router.navigate(['/employees']);
  }
}
