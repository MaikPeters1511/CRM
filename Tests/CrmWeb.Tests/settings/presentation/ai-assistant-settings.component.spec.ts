import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { AiAssistantSettingsComponent } from '../../../../Clients/crm-web/src/app/settings/presentation/ai-assistant-settings/ai-assistant-settings.component';
import { AiAssistantSettingsState } from '../../../../Clients/crm-web/src/app/settings/application/ai-assistant-settings.state';

describe('AiAssistantSettingsComponent', () => {
  it('shows a clean backend-unavailable notice instead of crashing when the ai-service endpoint does not exist yet', () => {
    const stateMock = {
      loading: () => false,
      backendUnavailable: () => true,
      hasErrors: () => false,
      errors: () => [],
      saving: () => false,
      settings: () => null,
      load: vi.fn(),
      save: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [AiAssistantSettingsComponent],
      providers: [
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
        { provide: AiAssistantSettingsState, useValue: stateMock },
      ],
    });

    const fixture = TestBed.createComponent(AiAssistantSettingsComponent);
    fixture.detectChanges();

    expect(stateMock.load).toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('.alert-warning')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('form')).toBeFalsy();
  });
});
