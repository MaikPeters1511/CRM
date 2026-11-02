import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { EmployeeStateService } from '../../application/employee-state.service';
import { AbsenceStateService } from '../../application/absence-state.service';
import { AbsenceType } from '../../domain/employee.model';

/**
 * Smart/Container-Component: Abwesenheit (Urlaub/Krankheit/Sonstige) anlegen.
 * Bildet die Konfliktwarnung des Backends ab (Terminkollision) und erfordert
 * bei Konflikt eine explizite Override-Bestaetigung (`confirmOverride`). Zeigt
 * zusaetzlich die eigenen Abwesenheitsanfragen inkl. Status unterhalb des Formulars
 * (Requirement "Eigene Abwesenheitsanfragen mit Status einsehen").
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
  protected readonly absenceState = inject(AbsenceStateService);

  private employeeId = '';

  protected readonly form = this.fb.nonNullable.group({
    type: ['Vacation' as AbsenceType, [Validators.required]],
    from: ['', [Validators.required]],
    to: ['', [Validators.required]],
    confirmOverride: [false],
  });

  ngOnInit(): void {
    this.employeeId = this.route.snapshot.paramMap.get('id') ?? '';
    this.absenceState.loadMyAbsences(this.employeeId);
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
      () => {
        this.form.reset({ type: 'Vacation', from: '', to: '', confirmOverride: false });
        this.absenceState.loadMyAbsences(this.employeeId);
      },
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

  statusLabelKey(status: string): string {
    return `employee.absence.myRequests.status.${status}`;
  }

  deleteAbsence(absenceId: string): void {
    this.absenceState.deleteAbsence?.(this.employeeId, absenceId);
  }
}
