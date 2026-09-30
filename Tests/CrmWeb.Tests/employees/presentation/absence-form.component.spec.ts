import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { AbsenceFormComponent } from '../../../../Clients/crm-web/src/app/employees/presentation/absence-form/absence-form.component';
import { EmployeeStateService } from '../../../../Clients/crm-web/src/app/employees/application/employee-state.service';

describe('AbsenceFormComponent', () => {
  function setup(stateOverrides: Record<string, unknown> = {}) {
    const stateMock = {
      hasErrors: () => false,
      errors: () => [],
      hasAbsenceConflict: () => false,
      absenceConflict: () => [],
      addAbsence: vi.fn(),
      clearAbsenceConflict: vi.fn(),
      ...stateOverrides,
    };

    TestBed.configureTestingModule({
      imports: [AbsenceFormComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: EmployeeStateService, useValue: stateMock },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'e1' } } } },
      ],
    });

    const fixture = TestBed.createComponent(AbsenceFormComponent);
    fixture.detectChanges();
    return { fixture, stateMock, component: fixture.componentInstance };
  }

  it('does not submit when from/to are missing', () => {
    const { stateMock, component } = setup();

    component.save();

    expect(stateMock.addAbsence).not.toHaveBeenCalled();
    expect(component['form'].invalid).toBe(true);
  });

  it('submits an absence for a conflict-free period (Urlaub eintragen)', () => {
    const { stateMock, component } = setup();
    component['form'].patchValue({ type: 'Vacation', from: '2026-11-01', to: '2026-11-05' });

    component.save();

    expect(stateMock.addAbsence).toHaveBeenCalledWith(
      'e1',
      { type: 'Vacation', from: '2026-11-01', to: '2026-11-05', confirmOverride: false },
      expect.any(Function),
    );
  });

  it('renders the conflict warning as role="alert" when the backend reports a collision', () => {
    const { fixture } = setup({
      hasAbsenceConflict: () => true,
      absenceConflict: () => [{ date: '2026-10-15', startTime: '10:00:00', endTime: '11:00:00' }],
    });

    const alert = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert).toBeTruthy();
    expect((alert.textContent as string)).toContain('2026-10-15');
  });

  it('retries with confirmOverride=true when the user confirms the conflict', () => {
    const { stateMock, component } = setup({
      hasAbsenceConflict: () => true,
      absenceConflict: () => [{ date: '2026-10-15', startTime: '10:00:00', endTime: '11:00:00' }],
    });
    component['form'].patchValue({ type: 'Vacation', from: '2026-10-15', to: '2026-10-15' });

    component.confirmOverrideAndRetry();

    expect(stateMock.addAbsence).toHaveBeenCalledWith(
      'e1',
      expect.objectContaining({ confirmOverride: true }),
      expect.any(Function),
    );
  });

  it('clears the conflict when the user cancels', () => {
    const { stateMock, component } = setup({ hasAbsenceConflict: () => true });

    component.cancelConflict();

    expect(stateMock.clearAbsenceConflict).toHaveBeenCalled();
  });
});
