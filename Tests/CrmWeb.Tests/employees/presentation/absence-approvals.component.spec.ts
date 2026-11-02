import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { AbsenceApprovalsComponent } from '../../../../Clients/crm-web/src/app/employees/presentation/absence-approvals/absence-approvals.component';
import { AbsenceStateService } from '../../../../Clients/crm-web/src/app/employees/application/absence-state.service';
import { PendingAbsence } from '../../../../Clients/crm-web/src/app/employees/domain/employee.model';

/**
 * Deckt die Requirements "Abwesenheitsanfrage genehmigen oder ablehnen" ab
 * (abwesenheit-workflow.openspec.md Abschnitt 3/4, Admin-Freigabe-Ansicht).
 */
describe('AbsenceApprovalsComponent', () => {
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

  function setup(stateOverrides: Record<string, unknown> = {}) {
    const stateMock = {
      pendingAbsences: () => [pendingEntry],
      loading: () => false,
      errors: () => [],
      hasErrors: () => false,
      loadPendingAbsences: vi.fn(),
      approve: vi.fn(),
      reject: vi.fn(),
      clearErrors: vi.fn(),
      ...stateOverrides,
    };

    TestBed.configureTestingModule({
      imports: [AbsenceApprovalsComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: AbsenceStateService, useValue: stateMock },
      ],
    });

    const fixture = TestBed.createComponent(AbsenceApprovalsComponent);
    fixture.detectChanges();
    return { fixture, stateMock, component: fixture.componentInstance };
  }

  it('loads the pending absence requests on init', () => {
    const { stateMock } = setup();

    expect(stateMock.loadPendingAbsences).toHaveBeenCalled();
  });

  it('renders each pending request with employee name, period and type', () => {
    const { fixture } = setup();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Erika');
    expect(text).toContain('Mustermann');
    expect(text).toContain('2026-11-01');
    expect(text).toContain('2026-11-05');
  });

  it('shows the empty state when there are no pending requests', () => {
    const { fixture } = setup({ pendingAbsences: () => [] });

    expect(fixture.nativeElement.textContent).toContain('employee.absenceApprovals.empty');
  });

  it('requires an explicit confirmation before approving (Bestaetigung)', () => {
    const { fixture, stateMock, component } = setup();

    component.requestApprove(pendingEntry);
    fixture.detectChanges();

    expect(stateMock.approve).not.toHaveBeenCalled();
    const confirmButton = fixture.nativeElement.querySelector('[data-testid="confirm-approve"]');
    expect(confirmButton).toBeTruthy();
  });

  it('approves the request once the confirmation is accepted (Scenario: Admin genehmigt eine ausstehende Anfrage)', () => {
    const { stateMock, component } = setup();

    component.requestApprove(pendingEntry);
    component.confirmDecision();

    expect(stateMock.approve).toHaveBeenCalledWith('e1', 'a1', expect.any(Function));
  });

  it('rejects the request once the confirmation is accepted (Scenario: Admin lehnt eine ausstehende Anfrage ab)', () => {
    const { stateMock, component } = setup();

    component.requestReject(pendingEntry);
    component.confirmDecision();

    expect(stateMock.reject).toHaveBeenCalledWith('e1', 'a1', expect.any(Function));
  });

  it('cancels the pending confirmation without calling approve/reject', () => {
    const { stateMock, component } = setup();

    component.requestApprove(pendingEntry);
    component.cancelDecision();

    expect(stateMock.approve).not.toHaveBeenCalled();
    expect(component.pendingDecision()).toBeNull();
  });

  it('has accessible approve/reject buttons with a non-empty aria-label', () => {
    const { fixture } = setup();

    const approveButton = fixture.nativeElement.querySelector('[data-testid="approve-a1"]');
    const rejectButton = fixture.nativeElement.querySelector('[data-testid="reject-a1"]');
    expect(approveButton.getAttribute('aria-label')).toBeTruthy();
    expect(rejectButton.getAttribute('aria-label')).toBeTruthy();
  });
});
