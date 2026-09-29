import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { CustomerFormComponent } from '../../../Clients/customer-web/src/app/presentation/customer-form/customer-form.component';
import { CustomerStateService } from '../../../Clients/customer-web/src/app/application/customer-state.service';

describe('CustomerFormComponent', () => {
  function setup(routeParamId: string | null = null) {
    const stateMock = {
      selectedCustomer: () => null,
      hasErrors: () => false,
      errors: () => [],
      loadCustomerById: vi.fn(),
      createCustomer: vi.fn(),
      updateCustomer: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [CustomerFormComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: CustomerStateService, useValue: stateMock },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => routeParamId } } },
        },
      ],
    });

    const fixture = TestBed.createComponent(CustomerFormComponent);
    fixture.detectChanges();
    return { fixture, stateMock, component: fixture.componentInstance };
  }

  it('shows a validation error and does not save when firstName/lastName are missing', () => {
    const { fixture, stateMock, component } = setup();

    component.save();
    fixture.detectChanges();

    expect(stateMock.createCustomer).not.toHaveBeenCalled();
    expect(component['form'].invalid).toBe(true);
    const firstNameError = fixture.nativeElement.querySelector('#firstName-error');
    expect(firstNameError).toBeTruthy();
  });

  it('requires either email or phone to be filled in', () => {
    const { component } = setup();
    component['form'].patchValue({ firstName: 'Max', lastName: 'Muster', email: '', phone: '' });

    expect(component['form'].invalid).toBe(true);
    expect(component['form'].errors?.['contactRequired']).toBe(true);
  });

  it('creates the customer when all mandatory fields are valid', () => {
    const { stateMock, component } = setup();
    component['form'].patchValue({
      firstName: 'Max',
      lastName: 'Muster',
      email: 'max@example.com',
      phone: '',
    });

    component.save();

    expect(stateMock.createCustomer).toHaveBeenCalledWith(
      expect.objectContaining({ firstName: 'Max', lastName: 'Muster', email: 'max@example.com' }),
      expect.any(Function),
    );
  });

  it('loads the existing customer and calls update in edit mode', () => {
    const { stateMock, component } = setup('c1');

    expect(stateMock.loadCustomerById).toHaveBeenCalledWith('c1');

    component['form'].patchValue({
      firstName: 'Erika',
      lastName: 'Mustermann',
      email: 'erika@example.com',
      phone: '',
    });
    component.save();

    expect(stateMock.updateCustomer).toHaveBeenCalledWith(
      'c1',
      expect.objectContaining({ firstName: 'Erika' }),
      expect.any(Function),
    );
  });
});
