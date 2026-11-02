import { Injectable, signal } from '@angular/core';
import { CustomerNote } from '../domain/customer-note.model';

/**
 * Application-Layer: Notizen zu Kunden. `customer-service` hat KEINE Notizen-API (siehe
 * Scope-Abgrenzung der Change-Spec) - Notizen leben daher nur im Speicher dieser Sitzung,
 * je Kunde, und sind NICHT persistiert (gehen beim Neuladen der Seite verloren).
 */
@Injectable({ providedIn: 'root' })
export class CustomerNotesState {
  private readonly _notesByCustomerId = signal<Record<string, CustomerNote[]>>({});

  notesFor(customerId: string): CustomerNote[] {
    return this._notesByCustomerId()[customerId] ?? [];
  }

  addNote(customerId: string, text: string): void {
    const trimmed = text.trim();
    if (!trimmed) {
      return;
    }
    const note: CustomerNote = {
      id: crypto.randomUUID(),
      text: trimmed,
      createdAt: new Date().toISOString(),
    };
    this._notesByCustomerId.update((byCustomer) => ({
      ...byCustomer,
      [customerId]: [note, ...(byCustomer[customerId] ?? [])],
    }));
  }
}
