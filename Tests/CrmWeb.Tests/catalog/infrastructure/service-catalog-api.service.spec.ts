import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ServiceCatalogApiService } from '../../../../Clients/crm-web/src/app/catalog/infrastructure/service-catalog-api.service';
import { environment } from '../../../../Clients/crm-web/src/environments/environment';

describe('ServiceCatalogApiService', () => {
  let service: ServiceCatalogApiService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.catalogApiBaseUrl}/services`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ServiceCatalogApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sends search requests with search/status/page query params', () => {
    service.search({ search: 'Beratung', status: true, page: 2 }).subscribe();

    const req = httpMock.expectOne(
      (r) =>
        r.url === baseUrl &&
        r.params.get('search') === 'Beratung' &&
        r.params.get('status') === 'true' &&
        r.params.get('page') === '2',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 2, pageSize: 20 });
  });

  it('omits status param when status filter is null', () => {
    service.search({ search: null, status: null, page: 1 }).subscribe();

    const req = httpMock.expectOne((r) => r.url === baseUrl);
    expect(req.request.params.has('status')).toBe(false);
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  });

  it('sends GET to /services/{id}', () => {
    service.getById('s1').subscribe();
    const req = httpMock.expectOne(`${baseUrl}/s1`);
    expect(req.request.method).toBe('GET');
    req.flush({});
  });

  it('sends POST to create a service', () => {
    const payload = { name: 'Beratung', description: null, defaultDurationMinutes: 30 };
    service.create(payload).subscribe();
    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({});
  });

  it('sends PUT to update a service', () => {
    const payload = { name: 'Beratung', description: null, defaultDurationMinutes: 30 };
    service.update('s1', payload).subscribe();
    const req = httpMock.expectOne(`${baseUrl}/s1`);
    expect(req.request.method).toBe('PUT');
    req.flush({});
  });

  it('sends POST to deactivate a service', () => {
    service.deactivate('s1').subscribe();
    const req = httpMock.expectOne(`${baseUrl}/s1/deactivate`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });
});
