import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AiAssistantSettingsState } from '../../../../Clients/crm-web/src/app/settings/application/ai-assistant-settings.state';
import { AiSettingsApiService } from '../../../../Clients/crm-web/src/app/settings/infrastructure/ai-settings-api.service';
import type { AiAssistantSettings } from '../../../../Clients/crm-web/src/app/settings/domain/settings.model';

describe('AiAssistantSettingsState', () => {
  function setup(apiMock: Partial<AiSettingsApiService>) {
    TestBed.configureTestingModule({
      providers: [{ provide: AiSettingsApiService, useValue: apiMock }],
    });
    return TestBed.inject(AiAssistantSettingsState);
  }

  it('loads and exposes the settings on success', () => {
    const settings: AiAssistantSettings = {
      enabled: true,
      model: 'gpt-5',
      temperature: 0.4,
      suggestionsEnabled: true,
    };
    const state = setup({ get: () => of(settings) });

    state.load();

    expect(state.settings()).toEqual(settings);
    expect(state.backendUnavailable()).toBe(false);
  });

  it('shows a clean backendUnavailable state instead of crashing when the ai-service endpoint does not exist yet (404)', () => {
    const state = setup({
      get: () => throwError(() => new HttpErrorResponse({ status: 404 })),
    });

    state.load();

    expect(state.backendUnavailable()).toBe(true);
    expect(state.settings()).toBeNull();
    expect(state.hasErrors()).toBe(false);
  });

  it('rolls back optimistic state and reports a validation error on save failure', () => {
    const state = setup({
      get: () =>
        of({ enabled: false, model: 'gpt-5', temperature: 0.2, suggestionsEnabled: false }),
      update: () =>
        throwError(
          () =>
            new HttpErrorResponse({
              status: 400,
              error: { errors: { temperature: ['Ungueltiger Wert'] } },
            }),
        ),
    });
    state.load();
    const onSuccess = vi.fn();

    state.save({ enabled: true, model: 'gpt-5', temperature: 5, suggestionsEnabled: true }, onSuccess);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(state.settings()?.enabled).toBe(false);
    expect(state.errors()).toEqual(['Ungueltiger Wert']);
  });
});
