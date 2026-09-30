import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, finalize, of, tap } from 'rxjs';
import {
  Appointment,
  AppointmentListItem,
  CalendarViewMode,
  CancelAppointmentRequest,
  CreateAppointmentRequest,
  RescheduleAppointmentRequest,
} from '../domain/appointment.model';
import { getCalendarRange, shiftAnchorDate, toListItem } from '../domain/calendar-range.util';
import { AppointmentApiService } from '../infrastructure/appointment-api.service';
import { extractValidationErrors, isConflict, isForbidden } from '../infrastructure/api-error.util';

function todayDateOnly(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * Application-Layer: zentraler State fuer die Kalenderansicht und die Terminverwaltung
 * (terminverwaltung.openspec.md Abschnitt 4). Haelt Kalenderausschnitt (Tag/Woche/Monat,
 * Anker-Datum), Mitarbeiterfilter und Terminliste als Signals. Kapselt die Use-Cases
 * Anlegen/Verschieben/Stornieren inkl. optimistischer Updates mit Rollback bei
 * Konfliktfehlern (409).
 */
@Injectable({ providedIn: 'root' })
export class CalendarStateService {
  private readonly api = inject(AppointmentApiService);

  private readonly _viewMode = signal<CalendarViewMode>('week');
  private readonly _anchorDate = signal<string>(todayDateOnly());
  private readonly _employeeFilter = signal<string | null>(null);
  private readonly _appointments = signal<AppointmentListItem[]>([]);
  private readonly _loading = signal(false);
  private readonly _errors = signal<string[]>([]);
  private readonly _conflict = signal(false);
  private readonly _selectedAppointment = signal<Appointment | null>(null);

  readonly selectedAppointment = this._selectedAppointment.asReadonly();
  readonly viewMode = this._viewMode.asReadonly();
  readonly anchorDate = this._anchorDate.asReadonly();
  readonly employeeFilter = this._employeeFilter.asReadonly();
  readonly appointments = this._appointments.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly errors = this._errors.asReadonly();
  readonly hasErrors = computed(() => this._errors().length > 0);
  readonly conflict = this._conflict.asReadonly();
  readonly range = computed(() => getCalendarRange(this._viewMode(), this._anchorDate()));

  setViewMode(mode: CalendarViewMode): void {
    this._viewMode.set(mode);
    this.loadAppointments();
  }

  setAnchorDate(date: string): void {
    this._anchorDate.set(date);
    this.loadAppointments();
  }

  goToToday(): void {
    this.setAnchorDate(todayDateOnly());
  }

  shift(direction: 1 | -1): void {
    this._anchorDate.set(shiftAnchorDate(this._viewMode(), this._anchorDate(), direction));
    this.loadAppointments();
  }

  setEmployeeFilter(employeeId: string | null): void {
    this._employeeFilter.set(employeeId);
    this.loadAppointments();
  }

  loadAppointments(): void {
    this._loading.set(true);
    this._errors.set([]);
    const { from, to } = this.range();

    this.api
      .getCalendar({ employeeId: this._employeeFilter(), customerId: null, from, to })
      .pipe(
        tap((items) => this._appointments.set(items)),
        catchError(() => {
          this._errors.set(['appointment.errors.loadFailed']);
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  loadAppointmentById(id: string): void {
    this._loading.set(true);
    this._errors.set([]);
    this.api
      .getById(id)
      .pipe(
        tap((appointment) => this._selectedAppointment.set(appointment)),
        catchError(() => {
          this._selectedAppointment.set(null);
          this._errors.set(['appointment.errors.loadFailed']);
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  createAppointment(request: CreateAppointmentRequest, onSuccess: (appointment: Appointment) => void): void {
    this._errors.set([]);
    this._conflict.set(false);

    this.api
      .create(request)
      .pipe(
        tap((appointment) => {
          this._appointments.update((list) => [...list, toListItem(appointment)]);
          onSuccess(appointment);
        }),
        catchError((error: unknown) => {
          if (isConflict(error)) {
            this._conflict.set(true);
          }
          const validationErrors = extractValidationErrors(error);
          this._errors.set(validationErrors.length ? validationErrors : ['appointment.errors.saveFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }

  rescheduleAppointment(
    id: string,
    request: RescheduleAppointmentRequest,
    onSuccess: (appointment: Appointment) => void,
    onForbidden?: () => void,
  ): void {
    this._errors.set([]);
    this._conflict.set(false);
    const previous = this._appointments();
    const previousSelected = this._selectedAppointment();
    const target = previous.find((a) => a.id === id);

    if (previousSelected?.id === id) {
      this._selectedAppointment.set({ ...previousSelected, start: request.newStart, end: request.newEnd });
    }

    if (target) {
      // Optimistic Update: Termin sofort im Kalender an die neue Zeit verschieben.
      this._appointments.update((list) =>
        list.map((a) =>
          a.id === id
            ? {
                ...a,
                start: request.newStart,
                end: request.newEnd,
                date: request.newStart.slice(0, 10),
                startTime: request.newStart.slice(11, 19),
                endTime: request.newEnd.slice(11, 19),
              }
            : a,
        ),
      );
    }

    this.api
      .reschedule(id, request)
      .pipe(
        tap((appointment) => {
          this._appointments.update((list) =>
            list.map((a) => (a.id === id ? toListItem(appointment) : a)),
          );
          if (this._selectedAppointment()?.id === id) {
            this._selectedAppointment.set(appointment);
          }
          onSuccess(appointment);
        }),
        catchError((error: unknown) => {
          // Rollback bei Konfliktfehler (409, Abschnitt 3 "Doppelbuchung verhindern"), RBAC-Verstoss
          // (403, Abschnitt 3 "Employee versucht Termin eines anderen Mitarbeiters zu verschieben")
          // oder sonstigem Fehler: Kalender zeigt wieder den vorherigen Zeitslot.
          this._appointments.set(previous);
          this._selectedAppointment.set(previousSelected);
          if (isForbidden(error)) {
            this._errors.set(['appointment.errors.forbidden']);
            onForbidden?.();
            return of(null);
          }
          if (isConflict(error)) {
            this._conflict.set(true);
          }
          const validationErrors = extractValidationErrors(error);
          this._errors.set(
            validationErrors.length ? validationErrors : ['appointment.errors.rescheduleFailed'],
          );
          return of(null);
        }),
      )
      .subscribe();
  }

  cancelAppointment(
    id: string,
    reason: string,
    onSuccess: (appointment: Appointment) => void,
    onForbidden?: () => void,
  ): void {
    this._errors.set([]);
    const request: CancelAppointmentRequest = { reason };
    const previous = this._appointments();
    const previousSelected = this._selectedAppointment();

    // Optimistic Update: Termin sofort als storniert markieren.
    this._appointments.update((list) =>
      list.map((a) => (a.id === id ? { ...a, status: 'Cancelled', cancellationReason: reason } : a)),
    );
    if (previousSelected?.id === id) {
      this._selectedAppointment.set({ ...previousSelected, status: 'Cancelled', cancellationReason: reason });
    }

    this.api
      .cancel(id, request)
      .pipe(
        tap((appointment) => {
          this._appointments.update((list) =>
            list.map((a) => (a.id === id ? toListItem(appointment) : a)),
          );
          if (this._selectedAppointment()?.id === id) {
            this._selectedAppointment.set(appointment);
          }
          onSuccess(appointment);
        }),
        catchError((error: unknown) => {
          // Rollback: Termin bleibt bestehen, wenn das Stornieren serverseitig fehlschlaegt
          // (inkl. RBAC-Verstoss/403, Abschnitt 3 "Employee versucht Termin eines anderen
          // Mitarbeiters zu stornieren").
          this._appointments.set(previous);
          this._selectedAppointment.set(previousSelected);
          if (isForbidden(error)) {
            this._errors.set(['appointment.errors.forbidden']);
            onForbidden?.();
          } else {
            this._errors.set(['appointment.errors.cancelFailed']);
          }
          return of(null);
        }),
      )
      .subscribe();
  }

  clearErrors(): void {
    this._errors.set([]);
    this._conflict.set(false);
  }
}
