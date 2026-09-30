/**
 * Schlankes Domain-Modell fuer den Kunde-Autocomplete im Terminformular. Bewusst kein
 * Cross-Import aus `customer-web` (ADR 0001: getrennte Frontends pro Microservice) - nur
 * die fuer die Terminvergabe benoetigten Felder aus `CustomerDto` (`customer-service`).
 */
export interface CustomerLookupItem {
  id: string;
  firstName: string;
  lastName: string;
  email: string | null;
  phone: string | null;
  isActive: boolean;
}

export interface CustomerSearchParams {
  search: string | null;
  status: boolean | null;
  page: number;
}

export interface CustomerSearchResult {
  items: CustomerLookupItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}
