import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, finalize, of, tap } from 'rxjs';
import {
  CreateServiceRequest,
  Service,
  ServiceFormValue,
  UpdateServiceRequest,
} from '../domain/service.model';
import { ServiceCatalogApiService } from '../infrastructure/service-catalog-api.service';
import { extractValidationErrors } from '../infrastructure/api-error.util';

/**
 * Application-Layer: zentraler State fuer den Leistungskatalog.
 * Haelt Leistungsliste, Suchfilter und ausgewaehlte Leistung als Signals.
 * Kapselt die Use-Cases (Suchen, Anlegen, Bearbeiten, Deaktivieren) inkl.
 * Fehler-Rollback bei fehlgeschlagenem Speichern (analog `CustomerStateService`).
 */
@Injectable({
  providedIn: 'root',
})
export class ServiceCatalogStateService {
  private readonly api = inject(ServiceCatalogApiService);

  private readonly _services = signal<Service[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _page = signal(1);
  private readonly _searchTerm = signal('');
  private readonly _statusFilter = signal<boolean | null>(true);
  private readonly _loading = signal(false);
  private readonly _errors = signal<string[]>([]);
  private readonly _selectedService = signal<Service | null>(null);

  readonly services = this._services.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly page = this._page.asReadonly();
  readonly searchTerm = this._searchTerm.asReadonly();
  readonly statusFilter = this._statusFilter.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly errors = this._errors.asReadonly();
  readonly selectedService = this._selectedService.asReadonly();
  readonly hasErrors = computed(() => this._errors().length > 0);

  loadServices(): void {
    this._loading.set(true);
    this._errors.set([]);
    this.api
      .search({
        search: this._searchTerm() || null,
        status: this._statusFilter(),
        page: this._page(),
      })
      .pipe(
        tap((result) => {
          this._services.set(result.items);
          this._totalCount.set(result.totalCount);
        }),
        catchError(() => {
          // Rollback: Liste bleibt unveraendert, nur Fehlermeldung wird gesetzt.
          this._errors.set(['catalog.errors.loadFailed']);
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  setSearchTerm(term: string): void {
    this._searchTerm.set(term);
    this._page.set(1);
    this.loadServices();
  }

  setStatusFilter(status: boolean | null): void {
    this._statusFilter.set(status);
    this._page.set(1);
    this.loadServices();
  }

  setPage(page: number): void {
    this._page.set(page);
    this.loadServices();
  }

  loadServiceById(id: string): void {
    this._loading.set(true);
    this._errors.set([]);
    this.api
      .getById(id)
      .pipe(
        tap((service) => this._selectedService.set(service)),
        catchError(() => {
          this._selectedService.set(null);
          this._errors.set(['catalog.errors.loadFailed']);
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  clearSelectedService(): void {
    this._selectedService.set(null);
  }

  createService(value: ServiceFormValue, onSuccess: (service: Service) => void): void {
    this._errors.set([]);
    const request: CreateServiceRequest = value;
    this.api
      .create(request)
      .pipe(
        tap((service) => {
          this._services.update((list) => [service, ...list]);
          onSuccess(service);
        }),
        catchError((error: unknown) => {
          // Kein Rollback notwendig: es wurde nichts optimistisch eingefuegt.
          const validationErrors = extractValidationErrors(error);
          this._errors.set(validationErrors.length ? validationErrors : ['catalog.errors.saveFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }

  updateService(id: string, value: ServiceFormValue, onSuccess: (service: Service) => void): void {
    this._errors.set([]);
    const previousList = this._services();
    const previousSelected = this._selectedService();

    // Optimistic Update
    this._services.update((list) =>
      list.map((service) => (service.id === id ? { ...service, ...value } : service)),
    );
    if (previousSelected?.id === id) {
      this._selectedService.set({ ...previousSelected, ...value });
    }

    const request: UpdateServiceRequest = value;
    this.api
      .update(id, request)
      .pipe(
        tap((service) => {
          this._services.update((list) => list.map((s) => (s.id === id ? service : s)));
          if (this._selectedService()?.id === id) {
            this._selectedService.set(service);
          }
          onSuccess(service);
        }),
        catchError((error: unknown) => {
          // Fehler-Rollback: vorherigen Listen- und Detailzustand wiederherstellen.
          this._services.set(previousList);
          this._selectedService.set(previousSelected);
          const validationErrors = extractValidationErrors(error);
          this._errors.set(validationErrors.length ? validationErrors : ['catalog.errors.saveFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }

  deactivateService(id: string, onSuccess: () => void): void {
    this._errors.set([]);
    const previousList = this._services();
    const previousSelected = this._selectedService();

    // Optimistic Update
    this._services.update((list) =>
      previousStatusFilterExcludesInactive(this._statusFilter())
        ? list.filter((service) => service.id !== id)
        : list.map((service) => (service.id === id ? { ...service, isActive: false } : service)),
    );
    if (previousSelected?.id === id) {
      this._selectedService.set({ ...previousSelected, isActive: false });
    }

    this.api
      .deactivate(id)
      .pipe(
        tap(() => onSuccess()),
        catchError(() => {
          // Fehler-Rollback: Leistung bleibt aktiv, vorheriger Zustand wird wiederherstellt.
          this._services.set(previousList);
          this._selectedService.set(previousSelected);
          this._errors.set(['catalog.errors.deactivateFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }
}

function previousStatusFilterExcludesInactive(status: boolean | null): boolean {
  return status === true;
}
