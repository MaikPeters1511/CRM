import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { CustomerListComponent } from '../../../../Clients/crm-web/src/app/customers/presentation/customer-list/customer-list.component';
import { CustomerStateService } from '../../../../Clients/crm-web/src/app/customers/application/customer-state.service';
import { Customer } from '../../../../Clients/crm-web/src/app/customers/domain/customer.model';

describe('CustomerListComponent', () => {
  const customers: Customer[] = [
    {
      id: '1',
      firstName: 'Anna',
      lastName: 'Zorn',
      email: 'anna@example.com',
      phone: null,
      address: null,
      isActive: true,
      createdAt: '2024-01-01T00:00:00Z',
      createdBy: 'tester',
      modifiedAt: null,
      modifiedBy: null,
    },
    {
      id: '2',
      firstName: 'Bert',
      lastName: 'Anders',
      email: 'bert@example.com',
      phone: null,
      address: null,
      isActive: true,
      createdAt: '2024-01-01T00:00:00Z',
      createdBy: 'tester',
      modifiedAt: null,
      modifiedBy: null,
    },
  ];

  function setup() {
    const stateMock = {
      customers: () => customers,
      totalCount: () => 2,
      page: () => 1,
      searchTerm: () => '',
      statusFilter: () => true,
      loading: () => false,
      errors: () => [],
      hasErrors: () => false,
      loadCustomers: vi.fn(),
      setSearchTerm: vi.fn(),
      setStatusFilter: vi.fn(),
      setPage: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [CustomerListComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: CustomerStateService, useValue: stateMock },
      ],
    });

    const fixture = TestBed.createComponent(CustomerListComponent);
    fixture.detectChanges();
    return { fixture, stateMock };
  }

  it('loads customers on init', () => {
    const { stateMock } = setup();
    expect(stateMock.loadCustomers).toHaveBeenCalled();
  });

  it('forwards search input to the state service (Suche nach Nachnamen)', () => {
    const { fixture, stateMock } = setup();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('#customer-search');
    input.value = 'Zorn';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(stateMock.setSearchTerm).toHaveBeenCalledWith('Zorn');
  });

  it('renders a row per customer with aria-sort on the sortable column', () => {
    const { fixture } = setup();
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);

    const header = fixture.nativeElement.querySelector('th[aria-sort]');
    expect(header.getAttribute('aria-sort')).toBe('none');
  });

  it('toggles aria-sort when the last-name header is clicked', () => {
    const { fixture } = setup();
    const sortButton: HTMLButtonElement = fixture.nativeElement.querySelector('th[aria-sort] button');
    sortButton.click();
    fixture.detectChanges();

    const header = fixture.nativeElement.querySelector('th[aria-sort]');
    expect(header.getAttribute('aria-sort')).toBe('ascending');
  });
});
