import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { ServiceCatalogStateService } from '../../../../Clients/crm-web/src/app/catalog/application/service-catalog-state.service';
import { ServiceCatalogApiService } from '../../../../Clients/crm-web/src/app/catalog/infrastructure/service-catalog-api.service';
import { Service } from '../../../../Clients/crm-web/src/app/catalog/domain/service.model';

/**
 * Deckt die Requirements aus Abschnitt 3 der Change-Spec ab:
 * Anlegen (gueltig/ungueltig), Suche, Bearbeiten, Deaktivieren inkl. Fehler-Rollback.
 */
describe('ServiceCatalogStateService', () => {
  const activeService: Service = {
    id: 's1',
    name: 'Beratung',
    description: 'Erstberatung',
    defaultDurationMinutes: 30,
    isActive: true,
    createdAt: '2024-01-01T00:00:00Z',
    createdBy: 'tester',
    modifiedAt: null,
    modifiedBy: null,
  };

  function createApiMock() {
    return {
      search: vi.fn().mockReturnValue(of({ items: [activeService], totalCount: 1, page: 1, pageSize: 20 })),
      getById: vi.fn().mockReturnValue(of(activeService)),
      create: vi.fn().mockReturnValue(of(activeService)),
      update: vi.fn().mockReturnValue(of(activeService)),
      deactivate: vi.fn().mockReturnValue(of(undefined)),
    };
  }

  function setup(apiOverrides: Partial<ReturnType<typeof createApiMock>> = {}) {
    const apiMock = { ...createApiMock(), ...apiOverrides };

    TestBed.configureTestingModule({
      providers: [ServiceCatalogStateService, { provide: ServiceCatalogApiService, useValue: apiMock }],
    });

    return {
      service: TestBed.inject(ServiceCatalogStateService),
      apiMock,
    };
  }

  it('loads services matching the search term (Suche nach Namen)', () => {
    const { service, apiMock } = setup();

    service.setSearchTerm('Beratung');

    expect(apiMock.search).toHaveBeenCalledWith(expect.objectContaining({ search: 'Beratung', page: 1 }));
    expect(service.services()).toEqual([activeService]);
    expect(service.totalCount()).toBe(1);
  });

  it('creates a service with a valid name and adds it to the list (Leistung anlegen)', () => {
    const { service, apiMock } = setup();
    const onSuccess = vi.fn();

    service.createService({ name: 'Beratung', description: null, defaultDurationMinutes: null }, onSuccess);

    expect(apiMock.create).toHaveBeenCalled();
    expect(onSuccess).toHaveBeenCalledWith(activeService);
    expect(service.services()[0]).toEqual(activeService);
    expect(service.hasErrors()).toBe(false);
  });

  it('does not persist and surfaces validation errors when create fails (Leistung ohne Namen)', () => {
    const validationError = new HttpErrorResponse({
      status: 400,
      error: { errors: { name: ['Name ist erforderlich.'] } },
    });
    const { service } = setup({ create: vi.fn().mockReturnValue(throwError(() => validationError)) });
    const onSuccess = vi.fn();

    service.createService({ name: '', description: null, defaultDurationMinutes: null }, onSuccess);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(service.services()).toEqual([]);
    expect(service.errors()).toEqual(['Name ist erforderlich.']);
  });

  it('updates a service and persists the change (Leistung aktualisieren)', () => {
    const { service, apiMock } = setup();
    service.setSearchTerm('');
    const onSuccess = vi.fn();
    const updated: Service = { ...activeService, name: 'Beratung Premium' };
    apiMock.update.mockReturnValue(of(updated));

    service.updateService(
      's1',
      { name: 'Beratung Premium', description: 'Erstberatung', defaultDurationMinutes: 30 },
      onSuccess,
    );

    expect(onSuccess).toHaveBeenCalledWith(updated);
    expect(service.services()[0].name).toBe('Beratung Premium');
  });

  it('rolls back the optimistic update when saving fails', () => {
    const { service, apiMock } = setup();
    service.setSearchTerm('');
    apiMock.update.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

    service.updateService('s1', { name: 'Kaputt', description: null, defaultDurationMinutes: null }, vi.fn());

    expect(service.services()[0].name).toBe('Beratung');
    expect(service.errors()).toEqual(['catalog.errors.saveFailed']);
  });

  it('deactivates a service and marks it inactive (Leistung deaktivieren)', () => {
    const { service } = setup();
    service.setStatusFilter(null);
    const onSuccess = vi.fn();

    service.deactivateService('s1', onSuccess);

    expect(onSuccess).toHaveBeenCalled();
    expect(service.services()[0].isActive).toBe(false);
  });

  it('rolls back deactivation when the backend call fails', () => {
    const { service, apiMock } = setup();
    service.setStatusFilter(null);
    apiMock.deactivate.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

    service.deactivateService('s1', vi.fn());

    expect(service.services()[0].isActive).toBe(true);
    expect(service.errors()).toEqual(['catalog.errors.deactivateFailed']);
  });

  it('loads a single service by id', () => {
    const { service, apiMock } = setup();

    service.loadServiceById('s1');

    expect(apiMock.getById).toHaveBeenCalledWith('s1');
    expect(service.selectedService()).toEqual(activeService);
  });
});
