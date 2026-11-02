/** Rollen eines Mitarbeiters. Entspricht `EmployeeRole` (Domain-Enum im Backend). */
export type EmployeeRole = 'Admin' | 'Employee';

/** Wochentag, entspricht `System.DayOfWeek`. */
export type Weekday =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday';

export const WEEKDAYS: Weekday[] = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
];

/** Art einer Abwesenheit. Entspricht `AbsenceType`. */
export type AbsenceType = 'Vacation' | 'Sick' | 'Other';

/**
 * Freigabestatus einer Abwesenheitsanfrage. Entspricht `AbsenceStatus`
 * (`employee-service`, abwesenheit-workflow.openspec.md Abschnitt 5). Serialisiert als
 * String (`JsonStringEnumConverter`, siehe `EmployeeService.Web/Program.cs`).
 */
export type AbsenceStatus = 'Pending' | 'Approved' | 'Rejected';

/** Value Object: Zuordnung einer Leistung (Service) zu einem Mitarbeiter. */
export interface EmployeeServiceAssignment {
  serviceId: string;
  serviceName: string;
}

/** Value Object: ein Arbeitszeit-Slot innerhalb der Woche. */
export interface WorkingHoursSlot {
  weekday: Weekday;
  /** Format `HH:mm:ss`, entspricht `TimeOnly`. */
  startTime: string;
  /** Format `HH:mm:ss`, entspricht `TimeOnly`. */
  endTime: string;
}

/** Entity: eine erfasste Abwesenheit, inkl. Freigabestatus (`AbsenceDto`). */
export interface Absence {
  id: string;
  type: AbsenceType;
  /** Format `yyyy-MM-dd`, entspricht `DateOnly`. */
  from: string;
  /** Format `yyyy-MM-dd`, entspricht `DateOnly`. */
  to: string;
  status: AbsenceStatus;
  requestedBy: string;
  requestedAt: string;
  decidedBy: string | null;
  decidedAt: string | null;
}

/** Ein ausstehender Abwesenheitsantrag in der Admin-Freigabe-Ansicht (`PendingAbsenceDto`). */
export interface PendingAbsence {
  employeeId: string;
  employeeFirstName: string;
  employeeLastName: string;
  absenceId: string;
  type: AbsenceType;
  /** Format `yyyy-MM-dd`, entspricht `DateOnly`. */
  from: string;
  /** Format `yyyy-MM-dd`, entspricht `DateOnly`. */
  to: string;
  requestedBy: string;
  requestedAt: string;
}

/** Eine genehmigte Abwesenheit (`ApprovedAbsenceDto`). */
export interface ApprovedAbsence {
  employeeId: string;
  employeeFirstName: string;
  employeeLastName: string;
  absenceId: string;
  type: AbsenceType;
  from: string;
  to: string;
}

/**
 * Domain-Entity: Mitarbeiter (Aggregate Root im Backend).
 * Zustandslose, unveraenderliche Repraesentation fuer das Frontend.
 * Entspricht `EmployeeDto` im `employee-service`.
 */
export interface Employee {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: EmployeeRole;
  isActive: boolean;
  services: EmployeeServiceAssignment[];
  workingHours: WorkingHoursSlot[];
  absences: Absence[];
  createdAt: string;
  createdBy: string;
  modifiedAt: string | null;
  modifiedBy: string | null;
}

/** Eingabedaten zum Anlegen eines Mitarbeiters (Formular-Payload). */
export interface CreateEmployeeFormValue {
  firstName: string;
  lastName: string;
  email: string;
  role: EmployeeRole;
}

/** Eingabedaten zum Bearbeiten eines Mitarbeiters inkl. Leistungs-Zuordnung. */
export interface UpdateEmployeeFormValue {
  firstName: string;
  lastName: string;
  email: string;
  role: EmployeeRole;
  services: EmployeeServiceAssignment[] | null;
}

/** Request-Shape fuer `POST /api/employees`. */
export type CreateEmployeeRequest = CreateEmployeeFormValue;

/** Request-Shape fuer `PUT /api/employees/{id}`. */
export type UpdateEmployeeRequest = UpdateEmployeeFormValue;

/** Such-/Filterparameter fuer die Mitarbeiterliste. */
export interface EmployeeSearchParams {
  search: string | null;
  role: EmployeeRole | null;
  serviceId: string | null;
  page: number;
}

/** Paginiertes Suchergebnis, entspricht `SearchEmployeesResult`. */
export interface EmployeeSearchResult {
  items: Employee[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Request-Shape fuer `PUT /api/employees/{id}/schedule`. */
export interface SetWorkingHoursRequest {
  slots: WorkingHoursSlot[];
}

/** Request-Shape fuer `POST /api/employees/{id}/absences`. */
export interface AddAbsenceRequest {
  type: AbsenceType;
  from: string;
  to: string;
  confirmOverride: boolean;
}

/** Ein bestehender Termin, der mit einer geplanten Abwesenheit kollidiert. */
export interface ExistingAppointmentSlot {
  date: string;
  startTime: string;
  endTime: string;
}

/** Ergebnis eines fehlgeschlagenen Abwesenheits-Requests wegen Terminkonflikt (HTTP 409). */
export interface AddAbsenceConflict {
  errors: string[];
  conflictingAppointments: ExistingAppointmentSlot[];
}
