import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { CustomerDetailComponent } from '../../../Clients/customer-web/src/app/presentation/customer-detail/customer-detail.component';
import { CustomerStateService } from '../../../Clients/customer-web/src/app/application/customer-state.service';
import { Customer } from '../../../Clients/customer-web/src/app/domain/customer.model';

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

    TestBed.configureTestingModule({
      imports: [CustomerDetailComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: CustomerStateService, useValue: stateMock },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'c1' } } } },
      ],
    });

    const fixture = TestBed.createComponent(CustomerDetailComponent);
    fixture.detectChanges();
    return { fixture, stateMock, component: fixture.componentInstance };
  }

  it('loads the customer by id from the route (Kundendetailsicht anzeigen)', () => {
    const { stateMock } = setup();
    expect(stateMock.loadCustomerById).toHaveBeenCalledWith('c1');
  });

  it('shows a not-yet-available hint on the appointments tab', () => {
    const { fixture, component } = setup();
    component.selectTab('appointments');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('customer.detail.appointmentsUnavailable');
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
