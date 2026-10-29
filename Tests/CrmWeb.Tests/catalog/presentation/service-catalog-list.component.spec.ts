import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { ServiceCatalogListComponent } from '../../../../Clients/crm-web/src/app/catalog/presentation/service-catalog-list/service-catalog-list.component';
import { ServiceCatalogStateService } from '../../../../Clients/crm-web/src/app/catalog/application/service-catalog-state.service';
import { Service } from '../../../../Clients/crm-web/src/app/catalog/domain/service.model';

describe('ServiceCatalogListComponent', () => {
  const services: Service[] = [
    {
      id: '1',
      name: 'Beratung',
      description: 'Erstberatung',
      defaultDurationMinutes: 30,
      isActive: true,
      createdAt: '2024-01-01T00:00:00Z',
      createdBy: 'tester',
      modifiedAt: null,
      modifiedBy: null,
    },
    {
      id: '2',
      name: 'Haarschnitt',
      description: null,
      defaultDurationMinutes: 45,
      isActive: true,
      createdAt: '2024-01-01T00:00:00Z',
      createdBy: 'tester',
      modifiedAt: null,
      modifiedBy: null,
    },
  ];

  function setup() {
    const stateMock = {
      services: () => services,
      totalCount: () => 2,
      page: () => 1,
      searchTerm: () => '',
      statusFilter: () => true,
      loading: () => false,
      errors: () => [],
      hasErrors: () => false,
      loadServices: vi.fn(),
      setSearchTerm: vi.fn(),
      setStatusFilter: vi.fn(),
      setPage: vi.fn(),
      deactivateService: vi.fn((_id: string, onSuccess: () => void) => onSuccess()),
    };

    TestBed.configureTestingModule({
      imports: [ServiceCatalogListComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: ServiceCatalogStateService, useValue: stateMock },
      ],
    });

    const fixture = TestBed.createComponent(ServiceCatalogListComponent);
    fixture.detectChanges();
    return { fixture, stateMock };
  }

  it('loads services on init', () => {
    const { stateMock } = setup();
    expect(stateMock.loadServices).toHaveBeenCalled();
  });

  it('forwards search input to the state service (Suche nach Namen)', () => {
    const { fixture, stateMock } = setup();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('#service-search');
    input.value = 'Haarschnitt';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(stateMock.setSearchTerm).toHaveBeenCalledWith('Haarschnitt');
  });

  it('renders a row per service with aria-sort on the sortable column and a status badge', () => {
    const { fixture } = setup();
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);

    const header = fixture.nativeElement.querySelector('th[aria-sort]');
    expect(header.getAttribute('aria-sort')).toBe('none');
    expect(fixture.nativeElement.querySelector('.badge')).toBeTruthy();
  });

  it('opens the deactivate confirmation modal and deactivates on confirm (Leistung deaktivieren)', () => {
    const { fixture, stateMock, component } = (() => {
      const result = setup();
      return { ...result, component: result.fixture.componentInstance };
    })();

    component.openDeactivateModal(services[0]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeTruthy();

    component.confirmDeactivate();
    fixture.detectChanges();

    expect(stateMock.deactivateService).toHaveBeenCalledWith('1', expect.any(Function));
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeFalsy();
  });
});
