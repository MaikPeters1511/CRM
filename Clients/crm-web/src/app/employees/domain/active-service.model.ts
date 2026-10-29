/**
 * Schlankes Domain-Modell fuer die Leistungsauswahl im Mitarbeiterformular. Bewusst kein
 * Cross-Import aus dem `catalog`-Feature-Bereich (analog `appointments/domain/employee.model.ts`,
 * ADR 0001) - nur die fuer die Auswahl benoetigten Felder aus `ServiceDto` (`catalog-service`).
 */
export interface ActiveService {
  id: string;
  name: string;
}
