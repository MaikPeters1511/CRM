import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AppointmentApiService } from '../../../../Clients/crm-web/src/app/appointments/infrastructure/appointment-api.service';
import { environment } from '../../../../Clients/crm-web/src/environments/environment';

describe('AppointmentApiService', () => {
  let service: AppointmentApiService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.appointmentApiBaseUrl}/appointments`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AppointmentApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sends GET /api/appointments with employeeId/from/to query params (Kalenderansicht)', () => {
    service.getCalendar({ employeeId: 'e1', customerId: null, from: '2024-01-01', to: '2024-01-07' }).subscribe();

    const req = httpMock.expectOne(
      (r) =>
        r.url === baseUrl &&
        r.params.get('employeeId') === 'e1' &&
        r.params.get('from') === '2024-01-01' &&
        r.params.get('to') === '2024-01-07',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ appointments: [], absenceEntries: [] });
  });

  it('omits employeeId/customerId params when not set', () => {
    service.getCalendar({ employeeId: null, customerId: null, from: '2024-01-01', to: '2024-01-07' }).subscribe();

    const req = httpMock.expectOne((r) => r.url === baseUrl);
    expect(req.request.params.has('employeeId')).toBe(false);
    expect(req.request.params.has('customerId')).toBe(false);
    req.flush({ appointments: [], absenceEntries: [] });
  });

  it('returns the CalendarResponse shape with appointments and absenceEntries (Breaking Change: kein nacktes Array mehr)', () => {
    const absenceEntry = { employeeId: 'e1', type: 'Vacation', from: '2024-11-20', to: '2024-11-22' };
    let result: unknown;

    service
      .getCalendar({ employeeId: null, customerId: null, from: '2024-01-01', to: '2024-01-07' })
      .subscribe((response) => (result = response));

    const req = httpMock.expectOne((r) => r.url === baseUrl);
    req.flush({ appointments: [], absenceEntries: [absenceEntry] });

    expect(result).toEqual({ appointments: [], absenceEntries: [absenceEntry] });
  });

  it('sends GET to /appointments/{id}', () => {
    service.getById('a1').subscribe();
    const req = httpMock.expectOne(`${baseUrl}/a1`);
    expect(req.request.method).toBe('GET');
    req.flush({});
  });

  it('sends POST to create an appointment', () => {
    const payload = {
      customerId: 'c1',
      employeeId: 'e1',
      serviceId: 's1',
      start: '2024-01-01T10:00:00',
      end: '2024-01-01T11:00:00',
    };
    service.create(payload).subscribe();
    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({});
  });

  it('sends PUT to /appointments/{id}/reschedule', () => {
    const payload = { newStart: '2024-01-01T12:00:00', newEnd: '2024-01-01T13:00:00' };
    service.reschedule('a1', payload).subscribe();
    const req = httpMock.expectOne(`${baseUrl}/a1/reschedule`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(payload);
    req.flush({});
  });

  it('sends POST to /appointments/{id}/cancel', () => {
    service.cancel('a1', { reason: 'Kunde krank' }).subscribe();
    const req = httpMock.expectOne(`${baseUrl}/a1/cancel`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ reason: 'Kunde krank' });
    req.flush({});
  });
});
