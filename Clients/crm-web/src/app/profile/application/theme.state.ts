import { Injectable, effect, signal } from '@angular/core';
import { CustomTheme } from '../domain/theme.model';

const ACTIVE_THEME_KEY = 'crm.theme.active';
const CUSTOM_THEMES_KEY = 'crm.theme.custom';

/**
 * Application-Layer: Theme-Auswahl/-Verwaltung. Rein clientseitig - kein Backend-Anspruch.
 * Setzt `data-theme` auf `<html>`, damit DaisyUI das aktive Theme live anwendet (Angular
 * Signals + `effect()` statt direkter DOM-Manipulation in der Praesentation).
 */
@Injectable({ providedIn: 'root' })
export class ThemeState {
  private readonly _activeTheme = signal(this.loadActiveTheme());
  private readonly _customThemes = signal<CustomTheme[]>(this.loadCustomThemes());

  readonly activeTheme = this._activeTheme.asReadonly();
  readonly customThemes = this._customThemes.asReadonly();

  constructor() {
    effect(() => {
      document.documentElement.setAttribute('data-theme', this._activeTheme());
    });
  }

  selectTheme(themeId: string): void {
    this._activeTheme.set(themeId);
    try {
      localStorage.setItem(ACTIVE_THEME_KEY, themeId);
    } catch {
      // localStorage evtl. nicht verfuegbar - Auswahl bleibt dann nur fuer diese Sitzung aktiv.
    }
  }

  saveCustomTheme(theme: CustomTheme): void {
    this._customThemes.update((list) => [...list.filter((t) => t.id !== theme.id), theme]);
    try {
      localStorage.setItem(CUSTOM_THEMES_KEY, JSON.stringify(this._customThemes()));
    } catch {
      // siehe oben.
    }
  }

  private loadActiveTheme(): string {
    try {
      return localStorage.getItem(ACTIVE_THEME_KEY) ?? 'mpcrm';
    } catch {
      return 'mpcrm';
    }
  }

  private loadCustomThemes(): CustomTheme[] {
    try {
      const raw = localStorage.getItem(CUSTOM_THEMES_KEY);
      return raw ? (JSON.parse(raw) as CustomTheme[]) : [];
    } catch {
      return [];
    }
  }
}
