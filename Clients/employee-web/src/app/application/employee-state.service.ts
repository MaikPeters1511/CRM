import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, finalize, of, tap } from 'rxjs';
import {
  Absence,
  AddAbsenceRequest,
  CreateEmployeeFormValue,
  Employee,
  EmployeeRole,
  EmployeeSearchParams,
  ExistingAppointmentSlot,
  SetWorkingHoursRequest,
  UpdateEmployeeFormValue,
  WorkingHoursSlot,
} from '../domain/employee.model';
import { EmployeeApiService } from '../infrastructure/employee-api.service';
import { extractConflictingAppointments, extractValidationErrors, isConflict } from '../infrastructure/api-error.util';

/**
 * Application-Layer: zentraler State fuer die Mitarbeiterverwaltung.
 * Haelt Mitarbeiterliste, Such-/Rollen-/Leistungsfilter, ausgewaehlten
 * Mitarbeiter sowie Arbeitszeiten- und Abwesenheits-State (inkl.
 * Konfliktpruefung mit dem `appointment-service`) als Signals.
 * Kapselt die Use-Cases inkl. Fehler-Rollback bei fehlgeschlagenem Speichern.
 */
@Injectable({
  providedIn: 'root',
})
export class EmployeeStateService {
  private readonly api = inject(EmployeeApiService);

  private readonly _employees = signal<Employee[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _page = signal(1);
  private readonly _searchTerm = signal('');
  private readonly _roleFilter = signal<EmployeeRole | null>(null);
  private readonly _serviceIdFilter = signal<string | null>(null);
  private readonly _loading = signal(false);
  private readonly _errors = signal<string[]>([]);
  private readonly _selectedEmployee = signal<Employee | null>(null);

  /** Kollidierende Termine der letzten Abwesenheits-Anfrage, falls ein Konflikt gemeldet wurde. */
  private readonly _absenceConflict = signal<ExistingAppointmentSlot[]>([]);

  readonly employees = this._employees.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly page = this._page.asReadonly();
  readonly searchTerm = this._searchTerm.asReadonly();
  readonly roleFilter = this._roleFilter.asReadonly();
  readonly serviceIdFilter = this._serviceIdFilter.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly errors = this._errors.asReadonly();
  readonly selectedEmployee = this._selectedEmployee.asReadonly();
  readonly hasErrors = computed(() => this._errors().length > 0);
  readonly absenceConflict = this._absenceConflict.asReadonly();
  readonly hasAbsenceConflict = computed(() => this._absenceConflict().length > 0);

  loadEmployees(): void {
    this._loading.set(true);
    this._errors.set([]);
    const params: EmployeeSearchParams = {
      search: this._searchTerm() || null,
      role: this._roleFilter(),
      serviceId: this._serviceIdFilter(),
      page: this._page(),
    };
    this.api
      .search(params)
      .pipe(
        tap((result) => {
          this._employees.set(result.items);
          this._totalCount.set(result.totalCount);
        }),
        catchError(() => {
          // Rollback: Liste bleibt unveraendert, nur Fehlermeldung wird gesetzt.
          this._errors.set(['employee.errors.loadFailed']);
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  setSearchTerm(term: string): void {
    this._searchTerm.set(term);
    this._page.set(1);
    this.loadEmployees();
  }

  setRoleFilter(role: EmployeeRole | null): void {
    this._roleFilter.set(role);
    this._page.set(1);
    this.loadEmployees();
  }

  setServiceIdFilter(serviceId: string | null): void {
    this._serviceIdFilter.set(serviceId);
    this._page.set(1);
    this.loadEmployees();
  }

  setPage(page: number): void {
    this._page.set(page);
    this.loadEmployees();
  }

  loadEmployeeById(id: string): void {
    this._loading.set(true);
    this._errors.set([]);
    this.api
      .getById(id)
      .pipe(
        tap((employee) => this._selectedEmployee.set(employee)),
        catchError(() => {
          this._selectedEmployee.set(null);
          this._errors.set(['employee.errors.loadFailed']);
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  clearSelectedEmployee(): void {
    this._selectedEmployee.set(null);
  }

  createEmployee(value: CreateEmployeeFormValue, onSuccess: (employee: Employee) => void): void {
    this._errors.set([]);
    this.api
      .create(value)
      .pipe(
        tap((employee) => {
          this._employees.update((list) => [employee, ...list]);
          onSuccess(employee);
        }),
        catchError((error: unknown) => {
          // Kein Rollback notwendig: es wurde nichts optimistisch eingefuegt.
          const validationErrors = extractValidationErrors(error);
          this._errors.set(validationErrors.length ? validationErrors : ['employee.errors.saveFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }

  updateEmployee(id: string, value: UpdateEmployeeFormValue, onSuccess: (employee: Employee) => void): void {
    this._errors.set([]);
    const previousList = this._employees();
    const previousSelected = this._selectedEmployee();

    // Optimistic Update
    this._employees.update((list) =>
      list.map((employee) => (employee.id === id ? { ...employee, ...value, services: value.services ?? employee.services } : employee)),
    );
    if (previousSelected?.id === id) {
      this._selectedEmployee.set({ ...previousSelected, ...value, services: value.services ?? previousSelected.services });
    }

    this.api
      .update(id, value)
      .pipe(
        tap((employee) => {
          this._employees.update((list) => list.map((e) => (e.id === id ? employee : e)));
          if (this._selectedEmployee()?.id === id) {
            this._selectedEmployee.set(employee);
          }
          onSuccess(employee);
        }),
        catchError((error: unknown) => {
          // Fehler-Rollback: vorherigen Listen- und Detailzustand wiederherstellen.
          this._employees.set(previousList);
          this._selectedEmployee.set(previousSelected);
          const validationErrors = extractValidationErrors(error);
          this._errors.set(validationErrors.length ? validationErrors : ['employee.errors.saveFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }

  deactivateEmployee(id: string, onSuccess: () => void): void {
    this._errors.set([]);
    const previousList = this._employees();
    const previousSelected = this._selectedEmployee();

    // Optimistic Update
    this._employees.update((list) => list.map((employee) => (employee.id === id ? { ...employee, isActive: false } : employee)));
    if (previousSelected?.id === id) {
      this._selectedEmployee.set({ ...previousSelected, isActive: false });
    }

    this.api
      .deactivate(id)
      .pipe(
        tap(() => onSuccess()),
        catchError(() => {
          // Fehler-Rollback: Mitarbeiter bleibt aktiv, vorheriger Zustand wird wiederherstellt.
          this._employees.set(previousList);
          this._selectedEmployee.set(previousSelected);
          this._errors.set(['employee.errors.deactivateFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }

  setWorkingHours(id: string, slots: WorkingHoursSlot[], onSuccess: (employee: Employee) => void): void {
    this._errors.set([]);
    const previousSelected = this._selectedEmployee();

    this.api
      .setSchedule(id, { slots })
      .pipe(
        tap((employee) => {
          this._selectedEmployee.set(employee);
          this._employees.update((list) => list.map((e) => (e.id === id ? employee : e)));
          onSuccess(employee);
        }),
        catchError((error: unknown) => {
          // Fehler-Rollback: vorheriger Wochenplan bleibt erhalten.
          this._selectedEmployee.set(previousSelected);
          const validationErrors = extractValidationErrors(error);
          this._errors.set(validationErrors.length ? validationErrors : ['employee.errors.saveFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }

  /**
   * Legt eine Abwesenheit an. Meldet das Backend einen Terminkonflikt (409),
   * wird KEIN Fehler in `errors` gesetzt, sondern die kollidierenden Termine
   * in `absenceConflict` abgelegt, damit die UI eine explizite
   * Override-Bestaetigung (`confirmOverride`) einholen kann.
   */
  addAbsence(id: string, request: AddAbsenceRequest, onSuccess: (absence: Absence) => void): void {
    this._errors.set([]);
    this._absenceConflict.set([]);

    this.api
      .addAbsence(id, request)
      .pipe(
        tap((absence) => {
          this._employees.update((list) =>
            list.map((e) => (e.id === id ? { ...e, absences: [...e.absences, absence] } : e)),
          );
          if (this._selectedEmployee()?.id === id) {
            this._selectedEmployee.update((employee) =>
              employee ? { ...employee, absences: [...employee.absences, absence] } : employee,
            );
          }
          onSuccess(absence);
        }),
        catchError((error: unknown) => {
          if (isConflict(error)) {
            this._absenceConflict.set(extractConflictingAppointments(error));
            this._errors.set(['employee.absence.errors.conflict']);
            return of(null);
          }
          const validationErrors = extractValidationErrors(error);
          this._errors.set(validationErrors.length ? validationErrors : ['employee.errors.saveFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }

  clearAbsenceConflict(): void {
    this._absenceConflict.set([]);
    this._errors.set([]);
  }
}
