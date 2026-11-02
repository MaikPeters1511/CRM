import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { AssistantStateService } from '../../application/assistant-state.service';
import { AssistantSuggestionCardComponent } from '../assistant-suggestion-card/assistant-suggestion-card.component';

/**
 * Schwebender, einklappbarer Chat-Button + Chat-Panel (ki-terminassistent.openspec.md,
 * Abschnitt 4). Sichtbar fuer Rollen `Employee`/`Admin` (siehe Einbindung in `app.html`).
 * a11y: Panel mit `role="log"`/`aria-live="polite"` fuer neue Nachrichten, Eingabefeld mit
 * `<label>` (visuell ausgeblendet) + `aria-describedby`, vollstaendige Tastaturbedienbarkeit
 * (Oeffnen/Schliessen, Senden per Enter, Fokus-Management beim Oeffnen).
 */
@Component({
  selector: 'app-assistant-chat-widget',
  standalone: true,
  imports: [FormsModule, TranslatePipe, AssistantSuggestionCardComponent],
  templateUrl: './assistant-chat-widget.component.html',
})
export class AssistantChatWidgetComponent {
  protected readonly state = inject(AssistantStateService);

  @ViewChild('messageInput') private readonly messageInput?: ElementRef<HTMLInputElement>;

  draft = '';

  toggle(): void {
    this.state.toggle();
    if (this.state.isOpen()) {
      // Fokus-Management: Eingabefeld erhaelt den Fokus, sobald das Panel geoeffnet wird.
      setTimeout(() => this.messageInput?.nativeElement.focus());
    }
  }

  close(): void {
    this.state.close();
  }

  send(): void {
    if (!this.draft.trim()) {
      return;
    }
    this.state.sendMessage(this.draft);
    this.draft = '';
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    } else if (event.key === 'Escape') {
      this.close();
    }
  }
}
