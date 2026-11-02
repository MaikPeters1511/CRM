import { Component, OnInit, inject, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthStateService } from '../../../auth/application/auth-state.service';
import { SecuritySettingsState } from '../../../settings/application/security-settings.state';
import { BUILT_IN_THEMES } from '../../domain/theme.model';
import { ThemeState } from '../../application/theme.state';
import { AdminThemeEditorComponent } from '../admin-theme-editor/admin-theme-editor.component';

type ProfileTab = 'theme' | 'security';

/**
 * Smart/Container-Component: Profilbereich mit Theme-Auswahl (mehrere vordefinierte
 * DaisyUI-Themes + Admin-Theme-Editor) und MFA-/Passkey-Verwaltung gegen die
 * Identity-Security-API (`SecuritySettingsState`, Error-State falls Backend fehlt).
 */
@Component({
  selector: 'app-profile-page',
  standalone: true,
  imports: [TranslatePipe, AdminThemeEditorComponent],
  templateUrl: './profile-page.component.html',
})
export class ProfilePageComponent implements OnInit {
  protected readonly authState = inject(AuthStateService);
  protected readonly themeState = inject(ThemeState);
  protected readonly securityState = inject(SecuritySettingsState);

  protected readonly builtInThemes = BUILT_IN_THEMES;

  private readonly _activeTab = signal<ProfileTab>('theme');
  protected readonly activeTab = this._activeTab.asReadonly();

  ngOnInit(): void {
    this.securityState.load();
  }

  selectTab(tab: ProfileTab): void {
    this._activeTab.set(tab);
  }

  selectTheme(themeId: string): void {
    this.themeState.selectTheme(themeId);
  }

  revokeDevice(deviceId: string): void {
    this.securityState.revokeDevice(deviceId);
  }
}
