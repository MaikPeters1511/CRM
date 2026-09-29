import { Address } from './address.model';

/**
 * Domain-Entity: Kunde (Aggregate Root im Backend).
 * Zustandslose, unveränderliche Repräsentation für das Frontend.
 * Entspricht `CustomerDto` im `customer-service`.
 */
export interface Customer {
  id: string;
  firstName: string;
  lastName: string;
  email: string | null;
  phone: string | null;
  address: Address | null;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  modifiedAt: string | null;
  modifiedBy: string | null;
}

/** Eingabedaten zum Anlegen/Bearbeiten eines Kunden (Formular-Payload). */
export interface CustomerFormValue {
  firstName: string;
  lastName: string;
  email: string | null;
  phone: string | null;
  address: Address | null;
}

/** Request-Shape für `POST /api/customers` und `PUT /api/customers/{id}`. */
export type CreateCustomerRequest = CustomerFormValue;
export type UpdateCustomerRequest = CustomerFormValue;

/** Such-/Filterparameter für die Kundenliste. */
export interface CustomerSearchParams {
  search: string | null;
  status: boolean | null;
  page: number;
}

/** Paginiertes Suchergebnis, entspricht `SearchCustomersResult`. */
export interface CustomerSearchResult {
  items: Customer[];
  totalCount: number;
  page: number;
  pageSize: number;
}
