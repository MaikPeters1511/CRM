import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NotificationApiService } from '../../../../Clients/crm-web/src/app/employees/infrastructure/notification-api.service';
import { environment } from '../../../../Clients/crm-web/src/environments/environment';

describe('NotificationApiService', () => {
  let service: NotificationApiService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.employeeApiBaseUrl}/notifications`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(NotificationApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sends GET /api/notifications for the own notifications, newest first', () => {
    service.getNotifications().subscribe();
    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('sends POST to /api/notifications/{id}/read', () => {
    service.markAsRead('n1').subscribe();
    const req = httpMock.expectOne(`${baseUrl}/n1/read`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });
});
