import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { AiAssistantSettingsState } from '../../application/ai-assistant-settings.state';

/**
 * Smart/Container-Component: KI-Assistent-Einstellungen gegen die `ai-service`-Settings-API
 * (entsteht parallel, siehe `AiAssistantSettingsState`). Zeigt einen sauberen Error-State,
 * solange der Endpunkt dort noch nicht existiert.
 */
@Component({
  selector: 'app-ai-assistant-settings',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './ai-assistant-settings.component.html',
})
export class AiAssistantSettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  protected readonly state = inject(AiAssistantSettingsState);

  protected readonly form = this.fb.nonNullable.group({
    enabled: [false],
    model: [''],
    temperature: [0.3],
    suggestionsEnabled: [false],
  });

  private readonly _savedVisible = signal(false);
  protected readonly savedVisible = this._savedVisible.asReadonly();

  constructor() {
    effect(() => {
      const current = this.state.settings();
      if (current) {
        this.form.setValue(current);
      }
    });
  }

  ngOnInit(): void {
    this.state.load();
  }

  save(): void {
    this.state.save(this.form.getRawValue(), () => this._savedVisible.set(true));
  }
}
