import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { DashboardStateService } from '../../../../Clients/crm-web/src/app/dashboard/application/dashboard-state.service';
import { AppointmentApiService } from '../../../../Clients/crm-web/src/app/appointments/infrastructure/appointment-api.service';
import { CustomerApiService } from '../../../../Clients/crm-web/src/app/customers/infrastructure/customer-api.service';
import { EmployeeApiService } from '../../../../Clients/crm-web/src/app/employees/infrastructure/employee-api.service';
import { AppointmentListItem } from '../../../../Clients/crm-web/src/app/appointments/domain/appointment.model';

/**
 * Deckt UserStories/mp-crm-mockup-redesign.openspec.md ("Dashboard"-Design) ab: Das Dashboard
 * aggregiert ausschliesslich bereits vorhandene Backend-Daten (keine neuen Endpunkte) zu
 * Kennzahl-Kacheln und einer Liste anstehender Termine.
 */
describe('DashboardStateService', () => {
  const todayAppointment: AppointmentListItem = {
    id: 'a1',
    customerId: 'c1',
    employeeId: 'e1',
    serviceId: 's1',
    date: '2024-05-15',
    startTime: '10:00:00',
    endTime: '11:00:00',
    start: '2024-05-15T10:00:00+00:00',
    end: '2024-05-15T11:00:00+00:00',
    status: 'Confirmed',
    cancellationReason: null,
  };

  const openRequestAppointment: AppointmentListItem = {
    ...todayAppointment,
    id: 'a2',
    start: '2024-05-15T13:00:00+00:00',
    end: '2024-05-15T14:00:00+00:00',
    status: 'RequiresReview',
  };

  const cancelledAppointment: AppointmentListItem = {
    ...todayAppointment,
    id: 'a3',
    status: 'Cancelled',
  };

  function createAppointmentApiMock() {
    return {
      getCalendar: vi.fn().mockReturnValue(of([todayAppointment, openRequestAppointment, cancelledAppointment])),
    };
  }

  function createCustomerApiMock() {
    return {
      search: vi.fn().mockReturnValue(of({ items: [], totalCount: 42, page: 1, pageSize: 20 })),
    };
  }

  function createEmployeeApiMock() {
    return {
      search: vi.fn().mockReturnValue(of({ items: [], totalCount: 7, page: 1, pageSize: 20 })),
    };
  }

  function setup(overrides?: {
    appointmentApi?: Partial<ReturnType<typeof createAppointmentApiMock>>;
    customerApi?: Partial<ReturnType<typeof createCustomerApiMock>>;
    employeeApi?: Partial<ReturnType<typeof createEmployeeApiMock>>;
  }) {
    const appointmentApiMock = { ...createAppointmentApiMock(), ...overrides?.appointmentApi };
    const customerApiMock = { ...createCustomerApiMock(), ...overrides?.customerApi };
    const employeeApiMock = { ...createEmployeeApiMock(), ...overrides?.employeeApi };

    TestBed.configureTestingModule({
      providers: [
        DashboardStateService,
        { provide: AppointmentApiService, useValue: appointmentApiMock },
        { provide: CustomerApiService, useValue: customerApiMock },
        { provide: EmployeeApiService, useValue: employeeApiMock },
      ],
    });

    return {
      service: TestBed.inject(DashboardStateService),
      appointmentApiMock,
      customerApiMock,
      employeeApiMock,
    };
  }

  it('aggregates metrics from the existing appointment/customer/employee APIs without inventing new endpoints', () => {
    const { service } = setup();

    service.load();

    expect(service.metrics()).toEqual({
      appointmentsToday: 2,
      openRequests: 1,
      activeCustomers: 42,
      totalEmployees: 7,
    });
  });

  it('lists non-cancelled upcoming appointments sorted by start time', () => {
    const { service } = setup();

    service.load();

    const upcoming = service.upcomingAppointments();
    expect(upcoming.map((item) => item.id)).toEqual(['a1', 'a2']);
  });

  it('queries today only, with no employee/customer filter (role-based scoping stays with the backend)', () => {
    const { service, appointmentApiMock } = setup();

    service.load();

    expect(appointmentApiMock.getCalendar).toHaveBeenCalledWith(
      expect.objectContaining({ employeeId: null, customerId: null }),
    );
    const call = appointmentApiMock.getCalendar.mock.calls[0][0];
    expect(call.from).toBe(call.to);
  });

  it('sets a generic error and clears loading when a request fails', () => {
    const { service } = setup({
      appointmentApi: { getCalendar: vi.fn().mockReturnValue(throwError(() => new Error('boom'))) },
    });

    service.load();

    expect(service.error()).toBe('dashboard.errors.generic');
    expect(service.loading()).toBe(false);
  });
});
