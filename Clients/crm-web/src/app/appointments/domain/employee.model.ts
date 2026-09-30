/**
 * Schlankes Domain-Modell fuer die Mitarbeiter-/Leistungsauswahl und den Mitarbeiterfilter
 * der Kalenderansicht. Bewusst kein Cross-Import aus `employee-web` (ADR 0001) - nur die fuer
 * die Terminvergabe benoetigten Felder aus `EmployeeDto` (`employee-service`).
 */
export interface EmployeeServiceAssignment {
  serviceId: string;
  serviceName: string;
}

export interface EmployeeLookupItem {
  id: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  services: EmployeeServiceAssignment[];
}

export interface EmployeeSearchParams {
  search: string | null;
  page: number;
}

export interface EmployeeSearchResult {
  items: EmployeeLookupItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Ein freier Zeitslot, entspricht `AvailableSlotDto` (`employee-service`). */
export interface AvailableSlot {
  /** Format `yyyy-MM-dd`, entspricht `DateOnly`. */
  date: string;
  /** Format `HH:mm:ss`, entspricht `TimeOnly`. */
  startTime: string;
  /** Format `HH:mm:ss`, entspricht `TimeOnly`. */
  endTime: string;
}
