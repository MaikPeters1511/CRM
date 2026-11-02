import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { AssistantSuggestionCardComponent } from '../../../../Clients/crm-web/src/app/assistant/presentation/assistant-suggestion-card/assistant-suggestion-card.component';
import { AppointmentSuggestion } from '../../../../Clients/crm-web/src/app/assistant/domain/assistant.model';

describe('AssistantSuggestionCardComponent', () => {
  const suggestion: AppointmentSuggestion = {
    employeeId: 'e1',
    employeeName: 'Anna Beispiel',
    serviceId: 's1',
    serviceName: 'Farbe',
    customerId: 'c1',
    startTime: '2026-10-19T10:00:00Z',
    endTime: '2026-10-19T11:00:00Z',
  };

  function setup() {
    TestBed.configureTestingModule({
      imports: [AssistantSuggestionCardComponent],
      providers: [provideRouter([]), provideTranslateService()],
    });

    const fixture = TestBed.createComponent(AssistantSuggestionCardComponent);
    fixture.componentRef.setInput('suggestion', suggestion);
    fixture.detectChanges();
    return { fixture, router: TestBed.inject(Router) };
  }

  // Requirement "Vorschlag übernehmen und bestätigen": Deep-Link ins bestehende
  // Terminanlage-Formular mit vorausgefüllten Werten, keine automatische Buchung.
  it('navigates to /appointments/new with prefilled query params when "Termin anlegen" is clicked', () => {
    const { fixture, router } = setup();
    const navigateSpy = vi.spyOn(router, 'navigate');

    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    button.click();

    expect(navigateSpy).toHaveBeenCalledWith(
      ['/appointments/new'],
      expect.objectContaining({
        queryParams: expect.objectContaining({
          employeeId: 'e1',
          serviceId: 's1',
          customerId: 'c1',
          date: '2026-10-19',
          startTime: '10:00',
          endTime: '11:00',
        }),
      }),
    );
  });
});
