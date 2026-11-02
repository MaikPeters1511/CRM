/**
 * Domain-Entity: Notiz zu einem Kunden. `customer-service` bietet aktuell KEINE
 * Notizen-API - Notizen sind daher bewusst rein lokaler, NICHT persistierter UI-Zustand
 * (siehe `CustomerNotesState`), der beim Neuladen der Seite verloren geht.
 */
export interface CustomerNote {
  id: string;
  text: string;
  createdAt: string;
}
