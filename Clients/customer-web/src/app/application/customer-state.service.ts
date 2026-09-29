import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, finalize, of, tap } from 'rxjs';
import {
  CreateCustomerRequest,
  Customer,
  CustomerFormValue,
  UpdateCustomerRequest,
} from '../domain/customer.model';
import { CustomerApiService } from '../infrastructure/customer-api.service';
import { extractValidationErrors } from '../infrastructure/api-error.util';

/**
 * Application-Layer: zentraler State fuer die Kundenverwaltung.
 * Haelt Kundenliste, Suchfilter und ausgewaehlten Kunden als Signals.
 * Kapselt die Use-Cases (Suchen, Anlegen, Bearbeiten, Deaktivieren) inkl.
 * Fehler-Rollback bei fehlgeschlagenem Speichern.
 */
@Injectable({
  providedIn: 'root',
})
export class CustomerStateService {
  private readonly api = inject(CustomerApiService);

  private readonly _customers = signal<Customer[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _page = signal(1);
  private readonly _searchTerm = signal('');
  private readonly _statusFilter = signal<boolean | null>(true);
  private readonly _loading = signal(false);
  private readonly _errors = signal<string[]>([]);
  private readonly _selectedCustomer = signal<Customer | null>(null);

  readonly customers = this._customers.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly page = this._page.asReadonly();
  readonly searchTerm = this._searchTerm.asReadonly();
  readonly statusFilter = this._statusFilter.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly errors = this._errors.asReadonly();
  readonly selectedCustomer = this._selectedCustomer.asReadonly();
  readonly hasErrors = computed(() => this._errors().length > 0);

  loadCustomers(): void {
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
          this._customers.set(result.items);
          this._totalCount.set(result.totalCount);
        }),
        catchError(() => {
          // Rollback: Liste bleibt unveraendert, nur Fehlermeldung wird gesetzt.
          this._errors.set(['customer.errors.loadFailed']);
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  setSearchTerm(term: string): void {
    this._searchTerm.set(term);
    this._page.set(1);
    this.loadCustomers();
  }

  setStatusFilter(status: boolean | null): void {
    this._statusFilter.set(status);
    this._page.set(1);
    this.loadCustomers();
  }

  setPage(page: number): void {
    this._page.set(page);
    this.loadCustomers();
  }

  loadCustomerById(id: string): void {
    this._loading.set(true);
    this._errors.set([]);
    this.api
      .getById(id)
      .pipe(
        tap((customer) => this._selectedCustomer.set(customer)),
        catchError(() => {
          this._selectedCustomer.set(null);
          this._errors.set(['customer.errors.loadFailed']);
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  clearSelectedCustomer(): void {
    this._selectedCustomer.set(null);
  }

  createCustomer(value: CustomerFormValue, onSuccess: (customer: Customer) => void): void {
    this._errors.set([]);
    const request: CreateCustomerRequest = value;
    this.api
      .create(request)
      .pipe(
        tap((customer) => {
          this._customers.update((list) => [customer, ...list]);
          onSuccess(customer);
        }),
        catchError((error: unknown) => {
          // Kein Rollback notwendig: es wurde nichts optimistisch eingefuegt.
          const validationErrors = extractValidationErrors(error);
          this._errors.set(validationErrors.length ? validationErrors : ['customer.errors.saveFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }

  updateCustomer(id: string, value: CustomerFormValue, onSuccess: (customer: Customer) => void): void {
    this._errors.set([]);
    const previousList = this._customers();
    const previousSelected = this._selectedCustomer();

    // Optimistic Update
    this._customers.update((list) =>
      list.map((customer) => (customer.id === id ? { ...customer, ...value } : customer)),
    );
    if (previousSelected?.id === id) {
      this._selectedCustomer.set({ ...previousSelected, ...value });
    }

    const request: UpdateCustomerRequest = value;
    this.api
      .update(id, request)
      .pipe(
        tap((customer) => {
          this._customers.update((list) => list.map((c) => (c.id === id ? customer : c)));
          if (this._selectedCustomer()?.id === id) {
            this._selectedCustomer.set(customer);
          }
          onSuccess(customer);
        }),
        catchError((error: unknown) => {
          // Fehler-Rollback: vorherigen Listen- und Detailzustand wiederherstellen.
          this._customers.set(previousList);
          this._selectedCustomer.set(previousSelected);
          const validationErrors = extractValidationErrors(error);
          this._errors.set(validationErrors.length ? validationErrors : ['customer.errors.saveFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }

  deactivateCustomer(id: string, onSuccess: () => void): void {
    this._errors.set([]);
    const previousList = this._customers();
    const previousSelected = this._selectedCustomer();

    // Optimistic Update
    this._customers.update((list) =>
      previousStatusFilterExcludesInactive(this._statusFilter())
        ? list.filter((customer) => customer.id !== id)
        : list.map((customer) => (customer.id === id ? { ...customer, isActive: false } : customer)),
    );
    if (previousSelected?.id === id) {
      this._selectedCustomer.set({ ...previousSelected, isActive: false });
    }

    this.api
      .deactivate(id)
      .pipe(
        tap(() => onSuccess()),
        catchError(() => {
          // Fehler-Rollback: Kunde bleibt aktiv, vorheriger Zustand wird wiederherstellt.
          this._customers.set(previousList);
          this._selectedCustomer.set(previousSelected);
          this._errors.set(['customer.errors.deactivateFailed']);
          return of(null);
        }),
      )
      .subscribe();
  }
}

function previousStatusFilterExcludesInactive(status: boolean | null): boolean {
  return status === true;
}
