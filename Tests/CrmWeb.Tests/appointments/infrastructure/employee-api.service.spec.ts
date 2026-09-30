import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EmployeeApiService } from '../../../../Clients/crm-web/src/app/appointments/infrastructure/employee-api.service';
import { environment } from '../../../../Clients/crm-web/src/environments/environment';

describe('EmployeeApiService (Mitarbeiter-/Leistungsauswahl, Verfuegbarkeit)', () => {
  let service: EmployeeApiService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.employeeApiBaseUrl}/employees`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(EmployeeApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sends GET /api/employees with the search term', () => {
    service.search({ search: 'Muster', page: 1 }).subscribe();
    const req = httpMock.expectOne((r) => r.url === baseUrl && r.params.get('search') === 'Muster');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  });

  it('sends GET /api/employees/{id}/availability?from=&to= (Live-Verfuegbarkeitsanzeige)', () => {
    service.getAvailability('e1', '2024-01-01', '2024-01-01').subscribe();
    const req = httpMock.expectOne(
      (r) =>
        r.url === `${baseUrl}/e1/availability` &&
        r.params.get('from') === '2024-01-01' &&
        r.params.get('to') === '2024-01-01',
    );
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });
});
