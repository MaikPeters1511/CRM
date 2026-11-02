import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { CustomerDetailComponent } from '../../../../Clients/crm-web/src/app/customers/presentation/customer-detail/customer-detail.component';
import { CustomerStateService } from '../../../../Clients/crm-web/src/app/customers/application/customer-state.service';
import { AppointmentHistoryState } from '../../../../Clients/crm-web/src/app/customers/application/appointment-history.state';
import { CustomerNotesState } from '../../../../Clients/crm-web/src/app/customers/application/customer-notes.state';
import { Customer } from '../../../../Clients/crm-web/src/app/customers/domain/customer.model';

describe('CustomerDetailComponent', () => {
  const customer: Customer = {
    id: 'c1',
    firstName: 'Erika',
    lastName: 'Mustermann',
    email: 'erika@example.com',
    phone: null,
    address: null,
    isActive: true,
    createdAt: '2024-01-01T00:00:00Z',
    createdBy: 'tester',
    modifiedAt: null,
    modifiedBy: null,
  };

  function setup() {
    const stateMock = {
      loading: () => false,
      selectedCustomer: () => customer,
      errors: () => [],
      loadCustomerById: vi.fn(),
      deactivateCustomer: vi.fn((_id: string, onSuccess: () => void) => onSuccess()),
    };
    const historyStateMock = {
      loading: () => false,
      loadFailed: () => false,
      entries: () => [{ id: 'a1', date: '2026-01-10', startTime: '09:00:00', endTime: '09:30:00', status: 'Completed' }],
      loadForCustomer: vi.fn(),
    };
    const notesStateMock = {
      notesFor: vi.fn(() => []),
      addNote: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [CustomerDetailComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: CustomerStateService, useValue: stateMock },
        { provide: AppointmentHistoryState, useValue: historyStateMock },
        { provide: CustomerNotesState, useValue: notesStateMock },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'c1' } } } },
      ],
    });

    const fixture = TestBed.createComponent(CustomerDetailComponent);
    fixture.detectChanges();
    return { fixture, stateMock, historyStateMock, notesStateMock, component: fixture.componentInstance };
  }

  it('loads the customer by id from the route (Kundendetailsicht anzeigen)', () => {
    const { stateMock, historyStateMock } = setup();
    expect(stateMock.loadCustomerById).toHaveBeenCalledWith('c1');
    expect(historyStateMock.loadForCustomer).toHaveBeenCalledWith('c1');
  });

  it('shows the full appointment history (real data) on the appointments tab', () => {
    const { fixture, component } = setup();
    component.selectTab('appointments');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('2026-01-10');
  });

  it('adds a note on the notes tab and clears the input afterwards', () => {
    const { fixture, component, notesStateMock } = setup();
    component.selectTab('notes');
    fixture.detectChanges();

    component['newNoteText'].set('Rueckruf vereinbart');
    component.addNote();

    expect(notesStateMock.addNote).toHaveBeenCalledWith('c1', 'Rueckruf vereinbart');
    expect(component['newNoteText']()).toBe('');
  });

  it('opens the deactivate confirmation modal and deactivates on confirm (Kunden deaktivieren)', () => {
    const { fixture, stateMock, component } = setup();

    component.openDeactivateModal();
    fixture.detectChanges();
    const dialog = fixture.nativeElement.querySelector('[role="dialog"]');
    expect(dialog).toBeTruthy();

    component.confirmDeactivate();
    fixture.detectChanges();

    expect(stateMock.deactivateCustomer).toHaveBeenCalledWith('c1', expect.any(Function));
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeFalsy();
  });

  it('closes the modal on Escape', () => {
    const { fixture, component } = setup();
    component.openDeactivateModal();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeTruthy();

    component.closeDeactivateModal();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeFalsy();
  });
});
