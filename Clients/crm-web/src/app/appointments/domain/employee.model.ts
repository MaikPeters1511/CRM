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

/**
 * Die Mitarbeiter-/Leistungsauswahl des Terminformulars und der Mitarbeiterfilter der
 * Kalenderansicht bleiben echte `<select>`-Dropdowns (DOM-Anforderung der Playwright-E2E-Tests),
 * nutzen aber ein vorgeschaltetes, debounced Suchfeld gegen die echte serverseitige Suche von
 * `employee-service` (`SearchEmployeesQuery.Search`) statt - wie zuvor - ALLE aktiven Mitarbeiter
 * ueber einen `pageSize`-Override in einem Aufruf zu laden. Das skaliert nicht mehr ueber die
 * Standard-Seitengroesse (20) hinaus; `pageSize` wird daher bewusst nicht mehr unterstuetzt.
 */
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
