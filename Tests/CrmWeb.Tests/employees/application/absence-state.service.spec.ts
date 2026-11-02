import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { AbsenceStateService } from '../../../../Clients/crm-web/src/app/employees/application/absence-state.service';
import { EmployeeApiService } from '../../../../Clients/crm-web/src/app/employees/infrastructure/employee-api.service';
import { Absence, PendingAbsence } from '../../../../Clients/crm-web/src/app/employees/domain/employee.model';

/**
 * Deckt die Requirements "Eigene Abwesenheitsanfragen mit Status einsehen" und
 * "Abwesenheitsanfrage genehmigen oder ablehnen" ab (abwesenheit-workflow.openspec.md
 * Abschnitt 3).
 */
describe('AbsenceStateService', () => {
  const pendingAbsence: Absence = {
    id: 'a1',
    type: 'Vacation',
    from: '2026-11-01',
    to: '2026-11-05',
    status: 'Pending',
    requestedBy: 'e1',
    requestedAt: '2026-10-20T10:00:00Z',
    decidedBy: null,
    decidedAt: null,
  };

  const approvedAbsence: Absence = {
    ...pendingAbsence,
    id: 'a2',
    status: 'Approved',
    decidedBy: 'admin1',
    decidedAt: '2026-10-21T08:00:00Z',
  };

  const pendingEntry: PendingAbsence = {
    employeeId: 'e1',
    employeeFirstName: 'Erika',
    employeeLastName: 'Mustermann',
    absenceId: 'a1',
    type: 'Vacation',
    from: '2026-11-01',
    to: '2026-11-05',
    requestedBy: 'e1',
    requestedAt: '2026-10-20T10:00:00Z',
  };

  function createApiMock() {
    return {
      getAbsences: vi.fn().mockReturnValue(of([pendingAbsence, approvedAbsence])),
      getPendingAbsences: vi.fn().mockReturnValue(of([pendingEntry])),
      approveAbsence: vi.fn().mockReturnValue(of({ ...pendingAbsence, status: 'Approved' as const, decidedBy: 'admin1', decidedAt: '2026-10-22T00:00:00Z' })),
      rejectAbsence: vi.fn().mockReturnValue(of({ ...pendingAbsence, status: 'Rejected' as const, decidedBy: 'admin1', decidedAt: '2026-10-22T00:00:00Z' })),
    };
  }

  function setup(apiOverrides: Partial<ReturnType<typeof createApiMock>> = {}) {
    const apiMock = { ...createApiMock(), ...apiOverrides };
    TestBed.configureTestingModule({
      providers: [AbsenceStateService, { provide: EmployeeApiService, useValue: apiMock }],
    });
    return { service: TestBed.inject(AbsenceStateService), apiMock };
  }

  it('loads the own absences including status (Scenario: Mitarbeiter sieht eigene Antraege)', () => {
    const { service, apiMock } = setup();

    service.loadMyAbsences('e1');

    expect(apiMock.getAbsences).toHaveBeenCalledWith('e1');
    expect(service.myAbsences()).toEqual([pendingAbsence, approvedAbsence]);
  });

  it('surfaces a clean error when loading own absences fails', () => {
    const { service } = setup({ getAbsences: vi.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 }))) });

    service.loadMyAbsences('e1');

    expect(service.errors()).toEqual(['employee.errors.loadFailed']);
    expect(service.myAbsences()).toEqual([]);
  });

  it('loads all pending absence requests for the admin approval view', () => {
    const { service, apiMock } = setup();

    service.loadPendingAbsences();

    expect(apiMock.getPendingAbsences).toHaveBeenCalled();
    expect(service.pendingAbsences()).toEqual([pendingEntry]);
  });

  it('approves a pending request and removes it from the pending list (Scenario: Admin genehmigt eine ausstehende Anfrage)', () => {
    const { service, apiMock } = setup();
    service.loadPendingAbsences();
    const onSuccess = vi.fn();

    service.approve('e1', 'a1', onSuccess);

    expect(apiMock.approveAbsence).toHaveBeenCalledWith('e1', 'a1');
    expect(onSuccess).toHaveBeenCalled();
    expect(service.pendingAbsences()).toEqual([]);
  });

  it('rejects a pending request and removes it from the pending list (Scenario: Admin lehnt eine ausstehende Anfrage ab)', () => {
    const { service, apiMock } = setup();
    service.loadPendingAbsences();
    const onSuccess = vi.fn();

    service.reject('e1', 'a1', onSuccess);

    expect(apiMock.rejectAbsence).toHaveBeenCalledWith('e1', 'a1');
    expect(onSuccess).toHaveBeenCalled();
    expect(service.pendingAbsences()).toEqual([]);
  });

  it('surfaces a conflict error without mutating the pending list when the request was already decided (Scenario: Bereits entschiedene Anfrage erneut entscheiden)', () => {
    const conflictError = new HttpErrorResponse({ status: 409 });
    const { service, apiMock } = setup({ approveAbsence: vi.fn().mockReturnValue(throwError(() => conflictError)) });
    service.loadPendingAbsences();
    const onSuccess = vi.fn();

    service.approve('e1', 'a1', onSuccess);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(service.errors()).toEqual(['employee.absenceApprovals.errors.alreadyDecided']);
    expect(service.pendingAbsences()).toEqual([pendingEntry]);
  });
});
