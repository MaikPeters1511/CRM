import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { AssistantChatWidgetComponent } from '../../../../Clients/crm-web/src/app/assistant/presentation/assistant-chat-widget/assistant-chat-widget.component';
import { AssistantStateService } from '../../../../Clients/crm-web/src/app/assistant/application/assistant-state.service';

describe('AssistantChatWidgetComponent', () => {
  function setup(isOpen = false) {
    const stateMock = {
      isOpen: () => isOpen,
      messages: () => [],
      loading: () => false,
      error: () => null,
      open: vi.fn(),
      close: vi.fn(),
      toggle: vi.fn(),
      sendMessage: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [AssistantChatWidgetComponent],
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: AssistantStateService, useValue: stateMock },
      ],
    });

    const fixture = TestBed.createComponent(AssistantChatWidgetComponent);
    fixture.detectChanges();
    return { fixture, stateMock };
  }

  it('renders the toggle button with an accessible label', () => {
    const { fixture } = setup();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button[aria-expanded]');

    expect(button).toBeTruthy();
    expect(button.getAttribute('aria-expanded')).toBe('false');
  });

  it('renders the message log with role="log" and aria-live="polite" when open', () => {
    const { fixture } = setup(true);
    const log: HTMLElement = fixture.nativeElement.querySelector('[role="log"]');

    expect(log).toBeTruthy();
    expect(log.getAttribute('aria-live')).toBe('polite');
  });

  it('calls toggle() when the floating button is clicked', () => {
    const { fixture, stateMock } = setup();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button[aria-expanded]');

    button.click();

    expect(stateMock.toggle).toHaveBeenCalled();
  });

  it('sends the draft message on Enter and clears the input', () => {
    const { fixture, stateMock } = setup(true);
    const component = fixture.componentInstance;
    component.draft = 'Freier Termin für Farbe';

    component.onKeydown(new KeyboardEvent('keydown', { key: 'Enter' }));

    expect(stateMock.sendMessage).toHaveBeenCalledWith('Freier Termin für Farbe');
    expect(component.draft).toBe('');
  });

  it('closes the panel on Escape', () => {
    const { fixture, stateMock } = setup(true);

    fixture.componentInstance.onKeydown(new KeyboardEvent('keydown', { key: 'Escape' }));

    expect(stateMock.close).toHaveBeenCalled();
  });
});
