import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ServiceLookupApiService } from '../../../../Clients/crm-web/src/app/employees/infrastructure/service-lookup-api.service';
import { environment } from '../../../../Clients/crm-web/src/environments/environment';

describe('ServiceLookupApiService', () => {
  let service: ServiceLookupApiService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.catalogApiBaseUrl}/services`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ServiceLookupApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('fetches only active services for the assignment select (GET /api/services?status=true)', () => {
    service.getActiveServices().subscribe();

    const req = httpMock.expectOne((r) => r.url === baseUrl && r.params.get('status') === 'true');
    expect(req.request.method).toBe('GET');
    req.flush({
      items: [{ id: 's1', name: 'Beratung', description: null, defaultDurationMinutes: 30, isActive: true, createdAt: '', createdBy: '', modifiedAt: null, modifiedBy: null }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    });
  });

  it('maps the search result to the slim ActiveService shape', () => {
    let result: { id: string; name: string }[] | undefined;
    service.getActiveServices().subscribe((services) => (result = services));

    const req = httpMock.expectOne((r) => r.url === baseUrl);
    req.flush({
      items: [{ id: 's1', name: 'Beratung', description: null, defaultDurationMinutes: 30, isActive: true, createdAt: '', createdBy: '', modifiedAt: null, modifiedBy: null }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    });

    expect(result).toEqual([{ id: 's1', name: 'Beratung' }]);
  });
});
