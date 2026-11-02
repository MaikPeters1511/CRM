import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, finalize, of, tap } from 'rxjs';
import { AiAssistantSettings } from '../domain/settings.model';
import { AiSettingsApiService } from '../infrastructure/ai-settings-api.service';
import { extractValidationErrors, isBackendUnavailable } from '../infrastructure/api-error.util';

/**
 * Application-Layer: State fuer die KI-Assistent-Einstellungen gegen die `ai-service`-
 * Settings-API (entsteht parallel, siehe `AiSettingsApiService`). Solange der Endpunkt noch
 * nicht existiert, zeigt `backendUnavailable` einen sauberen Leerzustand statt eines Crashes.
 */
@Injectable({ providedIn: 'root' })
export class AiAssistantSettingsState {
  private readonly api = inject(AiSettingsApiService);

  private readonly _settings = signal<AiAssistantSettings | null>(null);
  private readonly _loading = signal(false);
  private readonly _saving = signal(false);
  private readonly _backendUnavailable = signal(false);
  private readonly _errors = signal<string[]>([]);

  readonly settings = this._settings.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly saving = this._saving.asReadonly();
  readonly backendUnavailable = this._backendUnavailable.asReadonly();
  readonly errors = this._errors.asReadonly();
  readonly hasErrors = computed(() => this._errors().length > 0);

  load(): void {
    this._loading.set(true);
    this._errors.set([]);
    this._backendUnavailable.set(false);
    this.api
      .get()
      .pipe(
        tap((settings) => this._settings.set(settings)),
        catchError((error: unknown) => {
          if (isBackendUnavailable(error)) {
            this._backendUnavailable.set(true);
          } else {
            this._errors.set(['settings.ai.errors.loadFailed']);
          }
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  save(value: AiAssistantSettings, onSuccess: () => void): void {
    this._saving.set(true);
    this._errors.set([]);
    const previous = this._settings();

    this.api
      .update(value)
      .pipe(
        tap((settings) => {
          this._settings.set(settings);
          onSuccess();
        }),
        catchError((error: unknown) => {
          this._settings.set(previous);
          if (isBackendUnavailable(error)) {
            this._backendUnavailable.set(true);
          } else {
            const validationErrors = extractValidationErrors(error);
            this._errors.set(validationErrors.length ? validationErrors : ['settings.ai.errors.saveFailed']);
          }
          return of(null);
        }),
        finalize(() => this._saving.set(false)),
      )
      .subscribe();
  }
}
