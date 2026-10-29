/**
 * Domain-Entity: Leistung/Service (Aggregate Root im Backend `catalog-service`).
 * Zustandslose, unveraenderliche Repraesentation fuer das Frontend.
 * Entspricht `ServiceDto` im `catalog-service`.
 */
export interface Service {
  id: string;
  name: string;
  description: string | null;
  defaultDurationMinutes: number | null;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  modifiedAt: string | null;
  modifiedBy: string | null;
}

/** Eingabedaten zum Anlegen/Bearbeiten einer Leistung (Formular-Payload). */
export interface ServiceFormValue {
  name: string;
  description: string | null;
  defaultDurationMinutes: number | null;
}

/** Request-Shape fuer `POST /api/services` und `PUT /api/services/{id}`. */
export type CreateServiceRequest = ServiceFormValue;
export type UpdateServiceRequest = ServiceFormValue;

/** Such-/Filterparameter fuer die Leistungsliste. */
export interface ServiceSearchParams {
  search: string | null;
  status: boolean | null;
  page: number;
}

/** Paginiertes Suchergebnis, entspricht dem Rueckgabewert von `GET /api/services`. */
export interface ServiceSearchResult {
  items: Service[];
  totalCount: number;
  page: number;
  pageSize: number;
}
