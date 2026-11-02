import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AssistantStateService } from '../../../../Clients/crm-web/src/app/assistant/application/assistant-state.service';
import { AssistantApiService } from '../../../../Clients/crm-web/src/app/assistant/infrastructure/assistant-api.service';

describe('AssistantStateService', () => {
  function createApiMock() {
    return {
      sendMessage: vi.fn().mockReturnValue(
        of({ reply: 'Hier ein Vorschlag.', suggestions: [] }),
      ),
      getHistory: vi.fn().mockReturnValue(of([])),
    };
  }

  function setup(apiOverrides: Partial<ReturnType<typeof createApiMock>> = {}) {
    const apiMock = { ...createApiMock(), ...apiOverrides };
    TestBed.configureTestingModule({
      providers: [AssistantStateService, { provide: AssistantApiService, useValue: apiMock }],
    });
    return { service: TestBed.inject(AssistantStateService), apiMock };
  }

  it('toggles panel visibility', () => {
    const { service } = setup();

    expect(service.isOpen()).toBe(false);
    service.toggle();
    expect(service.isOpen()).toBe(true);
    service.close();
    expect(service.isOpen()).toBe(false);
  });

  it('appends the user message and the assistant reply (Freitext-Anfrage)', () => {
    const { service, apiMock } = setup();

    service.sendMessage('Freier Termin für Farbe');

    expect(apiMock.sendMessage).toHaveBeenCalledWith(
      expect.objectContaining({ message: 'Freier Termin für Farbe' }),
    );
    expect(service.messages()).toEqual([
      { role: 'user', content: 'Freier Termin für Farbe', suggestions: [] },
      { role: 'assistant', content: 'Hier ein Vorschlag.', suggestions: [] },
    ]);
  });

  it('reuses the same clientseitig generated sessionId across messages', () => {
    const { service, apiMock } = setup();

    service.sendMessage('Erste Nachricht');
    service.sendMessage('Zweite Nachricht');

    const firstSessionId = apiMock.sendMessage.mock.calls[0][0].sessionId;
    const secondSessionId = apiMock.sendMessage.mock.calls[1][0].sessionId;
    expect(firstSessionId).toBeTruthy();
    expect(secondSessionId).toBe(firstSessionId);
  });

  it('ignores blank messages', () => {
    const { service, apiMock } = setup();

    service.sendMessage('   ');

    expect(apiMock.sendMessage).not.toHaveBeenCalled();
    expect(service.messages()).toEqual([]);
  });

  // Requirement "Resilienz bei Nichtverfügbarkeit der KI-API"
  it('sets a clear error message instead of crashing when the backend is unavailable', () => {
    const { service } = setup({
      sendMessage: vi.fn().mockReturnValue(throwError(() => new Error('network error'))),
    });

    service.sendMessage('Freier Termin für Farbe');

    expect(service.error()).toBe('assistant.errors.unavailable');
    expect(service.loading()).toBe(false);
  });

  it('resets messages, session and error state', () => {
    const { service } = setup({
      sendMessage: vi.fn().mockReturnValue(throwError(() => new Error('x'))),
    });
    service.sendMessage('Hallo');
    expect(service.error()).toBeTruthy();

    service.reset();

    expect(service.messages()).toEqual([]);
    expect(service.error()).toBeNull();
  });
});
