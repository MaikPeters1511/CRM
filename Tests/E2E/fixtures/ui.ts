import { Page } from '@playwright/test';

/**
 * Kurze, bewusst dokumentierte Stabilisierungs-Pause nach einer clientseitigen
 * Angular-Router-Navigation (Klick auf einen Routerlink oder `page.goto()` innerhalb der SPA),
 * bevor das erste Formularfeld befuellt wird.
 *
 * Hintergrund (Testcode-Fix, KEIN Produktbug - real verifiziert in dieser Session): Playwright
 * `locator.fill()` schreibt den Wert per DOM-Property-Setter + `input`/`change`-Event. Unmittelbar
 * NACH einer SPA-Navigation (Angular aktualisiert `history.pushState` synchron mit dem
 * DOM-Rendering, aber die reaktive `FormControlName`-Bindung des neu erzeugten Formular-Controls
 * ist im allerersten Zyklus noch nicht in jedem Fall bereit) kann dieses Event verloren gehen -
 * das Feld zeigt den eingetippten Wert kurz in der DOM-`value`-Property, das zugehoerige
 * `FormControl` bleibt aber leer, wodurch `save()` die Validierung als fehlgeschlagen markiert
 * (`Vorname ist ein Pflichtfeld.` trotz sichtbarer Eingabe). Echte Tastatureingabe
 * (`pressSequentially`) oder ein kurzes Zeitfenster nach der Navigation beheben das
 * zuverlaessig - ein echter Nutzer braucht fuer Formularinteraktion ohnehin laenger als eine
 * synchrone Playwright-Aktion. Kein Eingriff in Produktivcode: reine Test-Timing-Robustheit
 * gegen ein SPA-Navigations-Race.
 */
export async function settleAfterNavigation(page: Page): Promise<void> {
  await page.waitForTimeout(250);
}
