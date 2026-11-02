/**
 * Domain-Modelle der Terminverwaltung. Entsprechen den DTOs von `appointment-service`
 * (`AppointmentService.Application.Appointments.AppointmentDto` /
 * `AppointmentListItemDto`, terminverwaltung.openspec.md Abschnitt 5).
 */
export type AppointmentStatus = 'Confirmed' | 'Cancelled' | 'Completed' | 'RequiresReview';

export type CalendarViewMode = 'day' | 'week' | 'month';

/**
 * Eintrag der Kalenderansicht (`GET /api/appointments`). Enthaelt zusaetzlich zu den vollen
 * Termindaten die flachen Felder `date`/`startTime`/`endTime` fuer die zeitliche Positionierung
 * im Grid - siehe `AppointmentListItemDto` im Backend.
 */
export interface AppointmentListItem {
  id: string;
  customerId: string;
  employeeId: string;
  serviceId: string;
  /** Format `yyyy-MM-dd`, entspricht `DateOnly`. */
  date: string;
  /** Format `HH:mm:ss`, entspricht `TimeOnly`. */
  startTime: string;
  /** Format `HH:mm:ss`, entspricht `TimeOnly`. */
  endTime: string;
  start: string;
  end: string;
  status: AppointmentStatus;
  cancellationReason: string | null;
}

/** Volle Termindaten, entspricht `AppointmentDto`. */
export interface Appointment {
  id: string;
  customerId: string;
  employeeId: string;
  serviceId: string;
  start: string;
  end: string;
  status: AppointmentStatus;
  cancellationReason: string | null;
  createdAt: string;
  createdBy: string;
  modifiedAt: string | null;
  modifiedBy: string | null;
}

/** Request-Shape fuer `GET /api/appointments`. */
export interface CalendarQueryParams {
  employeeId: string | null;
  customerId: string | null;
  /** Format `yyyy-MM-dd`. */
  from: string;
  /** Format `yyyy-MM-dd`. */
  to: string;
}

/** Art einer Abwesenheit, entspricht `AbsenceType` (`employee-service`). */
export type CalendarAbsenceType = 'Vacation' | 'Sick' | 'Other';

/**
 * Genehmigte Abwesenheit als zusaetzlicher, nicht-persistierter Ganztages-Eintrag der
 * Kalenderantwort (`CalendarAbsenceEntryDto`, abwesenheit-workflow.openspec.md Abschnitt 5).
 * Getrennt von `AppointmentListItem` gehalten - optisch abgesetzt, kein Termin.
 */
export interface CalendarAbsenceEntry {
  employeeId: string;
  type: CalendarAbsenceType;
  /** Format `yyyy-MM-dd`, entspricht `DateOnly`. */
  from: string;
  /** Format `yyyy-MM-dd`, entspricht `DateOnly`. */
  to: string;
}

/**
 * Response-Shape von `GET /api/appointments` (`CalendarResponse`, Breaking Change ggue.
 * dem vorherigen nackten `AppointmentListItem[]` - siehe `AppointmentsController.GetCalendar`
 * im `appointment-service`).
 */
export interface CalendarResponse {
  appointments: AppointmentListItem[];
  absenceEntries: CalendarAbsenceEntry[];
}

/** Request-Shape fuer `POST /api/appointments`. */
export interface CreateAppointmentRequest {
  customerId: string;
  employeeId: string;
  serviceId: string;
  start: string;
  end: string;
}

/** Request-Shape fuer `PUT /api/appointments/{id}/reschedule`. */
export interface RescheduleAppointmentRequest {
  newStart: string;
  newEnd: string;
}

/** Request-Shape fuer `POST /api/appointments/{id}/cancel`. */
export interface CancelAppointmentRequest {
  reason: string;
}
