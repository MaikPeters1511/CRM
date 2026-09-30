import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CustomerApiService } from '../../../../Clients/crm-web/src/app/customers/infrastructure/customer-api.service';
import { environment } from '../../../../Clients/crm-web/src/environments/environment';

describe('CustomerApiService', () => {
  let service: CustomerApiService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.customerApiBaseUrl}/customers`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(CustomerApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sends search requests with search/status/page query params', () => {
    service.search({ search: 'Muster', status: true, page: 2 }).subscribe();

    const req = httpMock.expectOne(
      (r) => r.url === baseUrl && r.params.get('search') === 'Muster' && r.params.get('status') === 'true' && r.params.get('page') === '2',
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

  it('sends GET to /customers/{id}', () => {
    service.getById('c1').subscribe();
    const req = httpMock.expectOne(`${baseUrl}/c1`);
    expect(req.request.method).toBe('GET');
    req.flush({});
  });

  it('sends POST to create a customer', () => {
    const payload = { firstName: 'Max', lastName: 'Muster', email: null, phone: '0123', address: null };
    service.create(payload).subscribe();
    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({});
  });

  it('sends PUT to update a customer', () => {
    const payload = { firstName: 'Max', lastName: 'Muster', email: null, phone: '0123', address: null };
    service.update('c1', payload).subscribe();
    const req = httpMock.expectOne(`${baseUrl}/c1`);
    expect(req.request.method).toBe('PUT');
    req.flush({});
  });

  it('sends POST to deactivate a customer', () => {
    service.deactivate('c1').subscribe();
    const req = httpMock.expectOne(`${baseUrl}/c1/deactivate`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });
});
