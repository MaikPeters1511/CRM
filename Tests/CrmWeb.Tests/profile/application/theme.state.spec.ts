import { TestBed } from '@angular/core/testing';
import { ThemeState } from '../../../../Clients/crm-web/src/app/profile/application/theme.state';
import { installLocalStorageMock } from '../../support/local-storage.mock';

describe('ThemeState', () => {
  beforeEach(() => {
    installLocalStorageMock();
    document.documentElement.removeAttribute('data-theme');
  });

  it('defaults to the mpcrm theme and applies it to the <html> element', () => {
    const state = TestBed.inject(ThemeState);
    TestBed.flushEffects();

    expect(state.activeTheme()).toBe('mpcrm');
    expect(document.documentElement.getAttribute('data-theme')).toBe('mpcrm');
  });

  it('selects a theme, persists it and applies it to the <html> element', () => {
    const state = TestBed.inject(ThemeState);

    state.selectTheme('dark');
    TestBed.flushEffects();

    expect(state.activeTheme()).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem('crm.theme.active')).toBe('dark');
  });

  it('saves a custom theme into the persisted list', () => {
    const state = TestBed.inject(ThemeState);

    state.saveCustomTheme({ id: 't1', name: 'Herbst', primary: '#ff0000', accent: '#00ff00', background: '#ffffff', text: '#000000' });

    expect(state.customThemes().length).toBe(1);
    expect(state.customThemes()[0].name).toBe('Herbst');
    expect(JSON.parse(localStorage.getItem('crm.theme.custom')!).length).toBe(1);
  });
});
