import { Injectable, inject, signal } from '@angular/core';
import { catchError, finalize, of, tap } from 'rxjs';
import { DisplayChatMessage } from '../domain/assistant.model';
import { AssistantApiService } from '../infrastructure/assistant-api.service';

/**
 * Application-Layer: zentraler State fuer den KI-Terminassistenten (Signals, analog
 * `CustomerStateService`/`ServiceCatalogStateService`). Haelt Chatverlauf, Ladezustand
 * ("Assistent denkt nach…"), Fehlerzustand und Panel-Sichtbarkeit.
 */
@Injectable({
  providedIn: 'root',
})
export class AssistantStateService {
  private readonly api = inject(AssistantApiService);

  private readonly _isOpen = signal(false);
  private readonly _messages = signal<DisplayChatMessage[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _sessionId = signal<string | null>(null);

  readonly isOpen = this._isOpen.asReadonly();
  readonly messages = this._messages.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  open(): void {
    this._isOpen.set(true);
  }

  close(): void {
    this._isOpen.set(false);
  }

  toggle(): void {
    this._isOpen.update((open) => !open);
  }

  sendMessage(text: string): void {
    const trimmed = text.trim();
    if (!trimmed || this._loading()) {
      return;
    }

    this._error.set(null);
    this._messages.update((messages) => [...messages, { role: 'user', content: trimmed, suggestions: [] }]);
    this._loading.set(true);

    // Der Server liefert keine sessionId in der Chat-Antwort zurueck (siehe
    // ki-terminassistent.openspec.md Abschnitt 5, `{ reply, suggestions }`) - die Session wird
    // daher clientseitig erzeugt und fuer alle Folgenachrichten derselben Konversation wiederverwendet.
    if (!this._sessionId()) {
      this._sessionId.set(crypto.randomUUID());
    }

    this.api
      .sendMessage({ sessionId: this._sessionId(), message: trimmed })
      .pipe(
        tap((response) => {
          this._messages.update((messages) => [
            ...messages,
            { role: 'assistant', content: response.reply, suggestions: response.suggestions },
          ]);
        }),
        catchError(() => {
          // Requirement "Resilienz bei Nichtverfügbarkeit der KI-API": klare Fehlermeldung statt
          // Absturz - der Bediener kann stattdessen die klassische Kalenderansicht nutzen.
          this._error.set('assistant.errors.unavailable');
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  reset(): void {
    this._messages.set([]);
    this._sessionId.set(null);
    this._error.set(null);
  }
}
