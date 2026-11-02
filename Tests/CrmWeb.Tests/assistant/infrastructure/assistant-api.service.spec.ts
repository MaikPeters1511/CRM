import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { AssistantApiService } from '../../../../Clients/crm-web/src/app/assistant/infrastructure/assistant-api.service';

describe('AssistantApiService', () => {
  function setup() {
    const httpMock = { post: vi.fn().mockReturnValue(of({ reply: 'ok', suggestions: [] })), get: vi.fn().mockReturnValue(of([])) };
    TestBed.configureTestingModule({
      providers: [AssistantApiService, { provide: HttpClient, useValue: httpMock }],
    });
    return { service: TestBed.inject(AssistantApiService), httpMock };
  }

  it('posts the chat message to /assistant/chat', () => {
    const { service, httpMock } = setup();

    service.sendMessage({ sessionId: null, message: 'Freier Termin für Farbe' }).subscribe();

    expect(httpMock.post).toHaveBeenCalledWith(
      expect.stringContaining('/assistant/chat'),
      { sessionId: null, message: 'Freier Termin für Farbe' },
    );
  });

  it('gets the chat history for a session', () => {
    const { service, httpMock } = setup();

    service.getHistory('session-1').subscribe();

    expect(httpMock.get).toHaveBeenCalledWith(expect.stringContaining('/assistant/sessions/session-1/history'));
  });
});
