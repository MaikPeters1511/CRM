import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AppointmentHistoryState } from '../../../../Clients/crm-web/src/app/customers/application/appointment-history.state';
import { AppointmentHistoryApiService } from '../../../../Clients/crm-web/src/app/customers/infrastructure/appointment-history-api.service';

describe('AppointmentHistoryState', () => {
  function setup(apiMock: Partial<AppointmentHistoryApiService>) {
    TestBed.configureTestingModule({
      providers: [{ provide: AppointmentHistoryApiService, useValue: apiMock }],
    });
    return TestBed.inject(AppointmentHistoryState);
  }

  it('loads and sorts the appointment history newest-first', () => {
    const state = setup({
      getForCustomer: () =>
        of([
          { id: 'a1', date: '2026-01-01', startTime: '09:00:00', endTime: '09:30:00', status: 'Completed' },
          { id: 'a2', date: '2026-03-01', startTime: '10:00:00', endTime: '10:30:00', status: 'Confirmed' },
        ]),
    });

    state.loadForCustomer('c1');

    expect(state.entries().map((e) => e.id)).toEqual(['a2', 'a1']);
    expect(state.loadFailed()).toBe(false);
  });

  it('shows a load-failed state instead of crashing when appointment-service is unreachable', () => {
    const state = setup({
      getForCustomer: () => throwError(() => new HttpErrorResponse({ status: 503 })),
    });

    state.loadForCustomer('c1');

    expect(state.loadFailed()).toBe(true);
    expect(state.entries()).toEqual([]);
  });
});
