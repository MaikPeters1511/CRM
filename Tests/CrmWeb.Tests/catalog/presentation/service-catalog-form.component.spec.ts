import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { ServiceCatalogFormComponent } from '../../../../Clients/crm-web/src/app/catalog/presentation/service-catalog-form/service-catalog-form.component';
import { ServiceCatalogStateService } from '../../../../Clients/crm-web/src/app/catalog/application/service-catalog-state.service';

describe('ServiceCatalogFormComponent', () => {
  function setup(routeParamId: string | null = null) {
    const stateMock = {
      selectedService: () => null,
      hasErrors: () => false,
      errors: () => [],
      loadServiceById: vi.fn(),
      createService: vi.fn(),
      updateService: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [ServiceCatalogFormComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: ServiceCatalogStateService, useValue: stateMock },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => routeParamId } } },
        },
      ],
    });

    const fixture = TestBed.createComponent(ServiceCatalogFormComponent);
    fixture.detectChanges();
    return { fixture, stateMock, component: fixture.componentInstance };
  }

  it('shows a validation error and does not save when the name is missing (Leistung ohne Namen anlegen)', () => {
    const { fixture, stateMock, component } = setup();

    component.save();
    fixture.detectChanges();

    expect(stateMock.createService).not.toHaveBeenCalled();
    expect(component['form'].invalid).toBe(true);
    const nameError = fixture.nativeElement.querySelector('#name-error');
    expect(nameError).toBeTruthy();
  });

  it('creates the service with a valid name (Leistung mit gueltigem Namen anlegen)', () => {
    const { stateMock, component } = setup();
    component['form'].patchValue({ name: 'Beratung', description: 'Erstberatung', defaultDurationMinutes: 30 });

    component.save();

    expect(stateMock.createService).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'Beratung', description: 'Erstberatung', defaultDurationMinutes: 30 }),
      expect.any(Function),
    );
  });

  it('loads the existing service and calls update in edit mode (Leistung aktualisieren)', () => {
    const { stateMock, component } = setup('s1');

    expect(stateMock.loadServiceById).toHaveBeenCalledWith('s1');

    component['form'].patchValue({ name: 'Beratung Premium', description: '', defaultDurationMinutes: null });
    component.save();

    expect(stateMock.updateService).toHaveBeenCalledWith(
      's1',
      expect.objectContaining({ name: 'Beratung Premium' }),
      expect.any(Function),
    );
  });
});
