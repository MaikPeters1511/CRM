import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { AdminThemeEditorComponent } from '../../../../Clients/crm-web/src/app/profile/presentation/admin-theme-editor/admin-theme-editor.component';
import { ThemeState } from '../../../../Clients/crm-web/src/app/profile/application/theme.state';

describe('AdminThemeEditorComponent', () => {
  it('updates the live preview style signal as colors change', () => {
    TestBed.configureTestingModule({
      imports: [AdminThemeEditorComponent],
      providers: [provideTranslateService({ lang: 'de', fallbackLang: 'de' })],
    });

    const fixture = TestBed.createComponent(AdminThemeEditorComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component['primary'].set('#123456');
    fixture.detectChanges();

    expect(component['previewStyle']().backgroundColor).toBe(component['background']());
    expect(component['previewStyle']()['--preview-primary']).toBe('#123456');
  });

  it('saves a named custom theme into ThemeState and resets the name field', () => {
    TestBed.configureTestingModule({
      imports: [AdminThemeEditorComponent],
      providers: [provideTranslateService({ lang: 'de', fallbackLang: 'de' })],
    });

    const fixture = TestBed.createComponent(AdminThemeEditorComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const state = TestBed.inject(ThemeState);

    component['name'].set('Mein Theme');
    component.save();

    expect(state.customThemes().some((t) => t.name === 'Mein Theme')).toBe(true);
    expect(component['name']()).toBe('');
  });

  it('does not save when no name was entered', () => {
    TestBed.configureTestingModule({
      imports: [AdminThemeEditorComponent],
      providers: [provideTranslateService({ lang: 'de', fallbackLang: 'de' })],
    });
    const fixture = TestBed.createComponent(AdminThemeEditorComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const state = TestBed.inject(ThemeState);
    const before = state.customThemes().length;

    component.save();

    expect(state.customThemes().length).toBe(before);
  });
});
