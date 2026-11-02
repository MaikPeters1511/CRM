import { NgStyle } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ThemeState } from '../../application/theme.state';
import { CustomTheme } from '../../domain/theme.model';

/**
 * Presentation/Smart-Component: Admin-Theme-Editor. Farbwaehler fuer
 * Primär-/Akzent-/Hintergrund-/Text-Farbe mit echter Live-Vorschau (direkte Style-Bindings
 * auf eine Vorschau-Komponente statt globaler Theme-Umschaltung, da beliebige Hex-Farben
 * nicht vorkompilierten DaisyUI-Themes entsprechen). "Speichern" legt das Theme in einer
 * Liste in `localStorage` ab (`ThemeState`) - kein Backend-Anspruch.
 */
@Component({
  selector: 'app-admin-theme-editor',
  standalone: true,
  imports: [FormsModule, NgStyle, TranslatePipe],
  templateUrl: './admin-theme-editor.component.html',
})
export class AdminThemeEditorComponent {
  protected readonly state = inject(ThemeState);

  protected readonly name = signal('');
  protected readonly primary = signal('#0f6b63');
  protected readonly accent = signal('#b8860b');
  protected readonly background = signal('#ffffff');
  protected readonly text = signal('#1c2b29');

  protected readonly previewStyle = computed(() => ({
    '--preview-primary': this.primary(),
    '--preview-accent': this.accent(),
    '--preview-background': this.background(),
    '--preview-text': this.text(),
    backgroundColor: this.background(),
    color: this.text(),
  }));

  save(): void {
    if (!this.name().trim()) {
      return;
    }
    const theme: CustomTheme = {
      id: crypto.randomUUID(),
      name: this.name().trim(),
      primary: this.primary(),
      accent: this.accent(),
      background: this.background(),
      text: this.text(),
    };
    this.state.saveCustomTheme(theme);
    this.name.set('');
  }
}
