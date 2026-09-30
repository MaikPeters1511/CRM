import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { CustomerStateService } from '../../../../Clients/crm-web/src/app/customers/application/customer-state.service';
import { CustomerApiService } from '../../../../Clients/crm-web/src/app/customers/infrastructure/customer-api.service';
import { Customer } from '../../../../Clients/crm-web/src/app/customers/domain/customer.model';

/**
 * Deckt die Requirements aus Abschnitt 3 der Change-Spec ab:
 * Anlegen (gueltig/ungueltig), Suche, Bearbeiten, Deaktivieren inkl. Fehler-Rollback.
 */
describe('CustomerStateService', () => {
  const activeCustomer: Customer = {
    id: 'c1',
    firstName: 'Erika',
    lastName: 'Mustermann',
    email: 'erika@example.com',
    phone: null,
    address: null,
    isActive: true,
    createdAt: '2024-01-01T00:00:00Z',
    createdBy: 'tester',
    modifiedAt: null,
    modifiedBy: null,
  };

  function createApiMock() {
    return {
      search: vi.fn().mockReturnValue(of({ items: [activeCustomer], totalCount: 1, page: 1, pageSize: 20 })),
      getById: vi.fn().mockReturnValue(of(activeCustomer)),
      create: vi.fn().mockReturnValue(of(activeCustomer)),
      update: vi.fn().mockReturnValue(of(activeCustomer)),
      deactivate: vi.fn().mockReturnValue(of(undefined)),
    };
  }

  function setup(apiOverrides: Partial<ReturnType<typeof createApiMock>> = {}) {
    const apiMock = { ...createApiMock(), ...apiOverrides };

    TestBed.configureTestingModule({
      providers: [CustomerStateService, { provide: CustomerApiService, useValue: apiMock }],
    });

    return {
      service: TestBed.inject(CustomerStateService),
      apiMock,
    };
  }

  it('loads customers matching the search term (Suche nach Nachnamen)', () => {
    const { service, apiMock } = setup();

    service.setSearchTerm('Muster');

    expect(apiMock.search).toHaveBeenCalledWith(
      expect.objectContaining({ search: 'Muster', page: 1 }),
    );
    expect(service.customers()).toEqual([activeCustomer]);
    expect(service.totalCount()).toBe(1);
  });

  it('creates a customer with valid mandatory fields and adds it to the list', () => {
    const { service, apiMock } = setup();
    const onSuccess = vi.fn();

    service.createCustomer(
      { firstName: 'Max', lastName: 'Muster', email: 'max@example.com', phone: null, address: null },
      onSuccess,
    );

    expect(apiMock.create).toHaveBeenCalled();
    expect(onSuccess).toHaveBeenCalledWith(activeCustomer);
    expect(service.customers()[0]).toEqual(activeCustomer);
    expect(service.hasErrors()).toBe(false);
  });

  it('does not persist and surfaces validation errors when create fails (fehlende Pflichtfelder)', () => {
    const validationError = new HttpErrorResponse({
      status: 400,
      error: { errors: { '': ['Vorname ist erforderlich.'] } },
    });
    const { service, apiMock } = setup({ create: vi.fn().mockReturnValue(throwError(() => validationError)) });
    const onSuccess = vi.fn();

    service.createCustomer({ firstName: '', lastName: '', email: null, phone: null, address: null }, onSuccess);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(service.customers()).toEqual([]);
    expect(service.errors()).toEqual(['Vorname ist erforderlich.']);
  });

  it('updates a customer and persists the change', () => {
    const { service, apiMock } = setup();
    service.setSearchTerm('');
    const onSuccess = vi.fn();
    const updated: Customer = { ...activeCustomer, firstName: 'Erika-Neu' };
    apiMock.update.mockReturnValue(of(updated));

    service.updateCustomer(
      'c1',
      { firstName: 'Erika-Neu', lastName: 'Mustermann', email: 'erika@example.com', phone: null, address: null },
      onSuccess,
    );

    expect(onSuccess).toHaveBeenCalledWith(updated);
    expect(service.customers()[0].firstName).toBe('Erika-Neu');
  });

  it('rolls back the optimistic update when saving fails', () => {
    const { service, apiMock } = setup();
    service.setSearchTerm('');
    apiMock.update.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

    service.updateCustomer(
      'c1',
      { firstName: 'Kaputt', lastName: 'Mustermann', email: 'erika@example.com', phone: null, address: null },
      vi.fn(),
    );

    expect(service.customers()[0].firstName).toBe('Erika');
    expect(service.errors()).toEqual(['customer.errors.saveFailed']);
  });

  it('deactivates a customer and marks it inactive (Kunden deaktivieren)', () => {
    const { service } = setup();
    service.setStatusFilter(null);
    const onSuccess = vi.fn();

    service.deactivateCustomer('c1', onSuccess);

    expect(onSuccess).toHaveBeenCalled();
    expect(service.customers()[0].isActive).toBe(false);
  });

  it('rolls back deactivation when the backend call fails', () => {
    const { service, apiMock } = setup();
    service.setStatusFilter(null);
    apiMock.deactivate.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

    service.deactivateCustomer('c1', vi.fn());

    expect(service.customers()[0].isActive).toBe(true);
    expect(service.errors()).toEqual(['customer.errors.deactivateFailed']);
  });

  it('loads a single customer by id for the detail view', () => {
    const { service, apiMock } = setup();

    service.loadCustomerById('c1');

    expect(apiMock.getById).toHaveBeenCalledWith('c1');
    expect(service.selectedCustomer()).toEqual(activeCustomer);
  });
});
