import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, of, tap } from 'rxjs';
import { Absence, PendingAbsence } from '../domain/employee.model';
import { EmployeeApiService } from '../infrastructure/employee-api.service';
import { isConflict } from '../infrastructure/api-error.util';

/**
 * Application-Layer: eigene Abwesenheitsanfragen (inkl. Status) sowie die Admin-Pending-Liste
 * fuer die Freigabe-Ansicht (abwesenheit-workflow.openspec.md Abschnitt 3/4). Getrennt von
 * `EmployeeStateService` (zustaendig fuer das Anlegen/Konfliktpruefung einer Abwesenheit),
 * da unabhaengiger Use-Case-Bereich (Einsicht + Admin-Entscheidung statt Erfassung).
 */
@Injectable({ providedIn: 'root' })
export class AbsenceStateService {
  private readonly api = inject(EmployeeApiService);

  private readonly _myAbsences = signal<Absence[]>([]);
  private readonly _pendingAbsences = signal<PendingAbsence[]>([]);
  private readonly _loading = signal(false);
  private readonly _errors = signal<string[]>([]);

  readonly myAbsences = this._myAbsences.asReadonly();
  readonly pendingAbsences = this._pendingAbsences.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly errors = this._errors.asReadonly();
  readonly hasErrors = computed(() => this._errors().length > 0);

  /** Eigene Abwesenheitsanfragen inkl. Status (Requirement "Eigene Abwesenheitsanfragen mit Status einsehen"). */
  loadMyAbsences(employeeId: string): void {
    this._loading.set(true);
    this._errors.set([]);
    this.api
      .getAbsences(employeeId)
      .pipe(
        tap((absences) => this._myAbsences.set(absences)),
        catchError(() => {
          this._myAbsences.set([]);
          this._errors.set(['employee.errors.loadFailed']);
          return of(null);
        }),
      )
      .subscribe(() => this._loading.set(false));
  }

  /** Alle ausstehenden Anfragen fuer die Admin-Freigabe-Ansicht. */
  loadPendingAbsences(): void {
    this._loading.set(true);
    this._errors.set([]);
    this.api
      .getPendingAbsences()
      .pipe(
        tap((absences) => this._pendingAbsences.set(absences)),
        catchError(() => {
          this._pendingAbsences.set([]);
          this._errors.set(['employee.absenceApprovals.errors.loadFailed']);
          return of(null);
        }),
      )
      .subscribe(() => this._loading.set(false));
  }

  /** Genehmigt eine ausstehende Anfrage; entfernt sie bei Erfolg aus der Pending-Liste. */
  approve(employeeId: string, absenceId: string, onSuccess: (absence: Absence) => void): void {
    this._errors.set([]);
    this.api
      .approveAbsence(employeeId, absenceId)
      .pipe(
        tap((absence) => {
          this._pendingAbsences.update((list) => list.filter((a) => a.absenceId !== absenceId));
          onSuccess(absence);
        }),
        catchError((error: unknown) => {
          this._errors.set([
            isConflict(error)
              ? 'employee.absenceApprovals.errors.alreadyDecided'
              : 'employee.absenceApprovals.errors.decideFailed',
          ]);
          return of(null);
        }),
      )
      .subscribe();
  }

  /** Lehnt eine ausstehende Anfrage ab; entfernt sie bei Erfolg aus der Pending-Liste. */
  reject(employeeId: string, absenceId: string, onSuccess: (absence: Absence) => void): void {
    this._errors.set([]);
    this.api
      .rejectAbsence(employeeId, absenceId)
      .pipe(
        tap((absence) => {
          this._pendingAbsences.update((list) => list.filter((a) => a.absenceId !== absenceId));
          onSuccess(absence);
        }),
        catchError((error: unknown) => {
          this._errors.set([
            isConflict(error)
              ? 'employee.absenceApprovals.errors.alreadyDecided'
              : 'employee.absenceApprovals.errors.decideFailed',
          ]);
          return of(null);
        }),
      )
      .subscribe();
  }

  clearErrors(): void {
    this._errors.set([]);
  }
}
