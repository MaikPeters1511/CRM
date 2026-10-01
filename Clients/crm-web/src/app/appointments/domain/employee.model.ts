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
  /**
   * Optionale Seitengroesse. Die Mitarbeiter-/Leistungsauswahl des Terminformulars und der
   * Mitarbeiterfilter der Kalenderansicht sind einfache `<select>`-Dropdowns (kein Autocomplete
   * wie bei der Kundenauswahl) und muessen daher ALLE aktiven Mitarbeiter in einem Aufruf laden
   * - ohne diesen Parameter liefert `employee-service` serverseitig nur die Standard-Seitengroesse
   * (20), wodurch Mitarbeiter ab Position 21 im Dropdown fehlen (nicht auswaehlbar/filterbar).
   * `EMPLOYEE_LOOKUP_PAGE_SIZE` deckt die in der Praxis realistische Mitarbeiterzahl pro
   * Unternehmen ab, ohne Infinite-Scroll/Pagination-UI fuer ein simples Auswahlfeld einzufuehren.
   */
  pageSize?: number;
}

/** Seitengroesse fuer die vollstaendige Mitarbeiter-Lookup-Liste (Terminformular/Kalenderfilter). */
export const EMPLOYEE_LOOKUP_PAGE_SIZE = 200;

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
