import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CustomerApiService } from '../../../../Clients/crm-web/src/app/appointments/infrastructure/customer-api.service';
import { environment } from '../../../../Clients/crm-web/src/environments/environment';

describe('CustomerApiService (Kunde-Autocomplete)', () => {
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

  it('sends GET /api/customers with the search term', () => {
    service.search({ search: 'Muster', status: true, page: 1 }).subscribe();

    const req = httpMock.expectOne(
      (r) => r.url === baseUrl && r.params.get('search') === 'Muster' && r.params.get('status') === 'true',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  });
});
