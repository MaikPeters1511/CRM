import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EmployeeApiService } from '../../../Clients/employee-web/src/app/infrastructure/employee-api.service';
import { environment } from '../../../Clients/employee-web/src/environments/environment';

describe('EmployeeApiService', () => {
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

  it('sends search requests with search/role/serviceId/page query params', () => {
    service.search({ search: 'Muster', role: 'Admin', serviceId: 's1', page: 2 }).subscribe();

    const req = httpMock.expectOne(
      (r) =>
        r.url === baseUrl &&
        r.params.get('search') === 'Muster' &&
        r.params.get('role') === 'Admin' &&
        r.params.get('serviceId') === 's1' &&
        r.params.get('page') === '2',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, page: 2, pageSize: 20 });
  });

  it('omits role/serviceId params when filters are null', () => {
    service.search({ search: null, role: null, serviceId: null, page: 1 }).subscribe();

    const req = httpMock.expectOne((r) => r.url === baseUrl);
    expect(req.request.params.has('role')).toBe(false);
    expect(req.request.params.has('serviceId')).toBe(false);
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  });

  it('sends GET to /employees/{id}', () => {
    service.getById('e1').subscribe();
    const req = httpMock.expectOne(`${baseUrl}/e1`);
    expect(req.request.method).toBe('GET');
    req.flush({});
  });

  it('sends POST to create an employee', () => {
    const payload = { firstName: 'Max', lastName: 'Muster', email: 'max@example.com', role: 'Employee' as const };
    service.create(payload).subscribe();
    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({});
  });

  it('sends PUT to update an employee including service assignments', () => {
    const payload = {
      firstName: 'Max',
      lastName: 'Muster',
      email: 'max@example.com',
      role: 'Employee' as const,
      services: [{ serviceId: 's1', serviceName: 'Beratung' }],
    };
    service.update('e1', payload).subscribe();
    const req = httpMock.expectOne(`${baseUrl}/e1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(payload);
    req.flush({});
  });

  it('sends POST to deactivate an employee', () => {
    service.deactivate('e1').subscribe();
    const req = httpMock.expectOne(`${baseUrl}/e1/deactivate`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('sends PUT to set the weekly schedule', () => {
    const request = { slots: [{ weekday: 'Monday' as const, startTime: '09:00:00', endTime: '17:00:00' }] };
    service.setSchedule('e1', request).subscribe();
    const req = httpMock.expectOne(`${baseUrl}/e1/schedule`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush({});
  });

  it('sends POST to add an absence', () => {
    const request = { type: 'Vacation' as const, from: '2026-10-15', to: '2026-10-20', confirmOverride: false };
    service.addAbsence('e1', request).subscribe();
    const req = httpMock.expectOne(`${baseUrl}/e1/absences`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({});
  });
});
