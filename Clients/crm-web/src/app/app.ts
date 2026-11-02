import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthStateService } from './auth/application/auth-state.service';
import { AssistantStateService } from './assistant/application/assistant-state.service';
import { AssistantChatWidgetComponent } from './assistant/presentation/assistant-chat-widget/assistant-chat-widget.component';
import { NotificationBellComponent } from './employees/presentation/notification-bell/notification-bell.component';
import { ThemeState } from './profile/application/theme.state';
import { BUILT_IN_THEMES, BuiltInTheme } from './profile/domain/theme.model';

/**
 * Shell-Komponente: Sidebar-Navigation/Router-Outlet fuer die konsolidierte Anwendung
 * (MP-CRM-Mockup-Redesign, UserStories/mp-crm-mockup-redesign.openspec.md). Zeigt die
 * gemeinsame Navigation (Dashboard/Kunden/Mitarbeiter/Termine/Leistungen/KI-Assistent) fuer
 * eingeloggte Nutzer. Beide unterstuetzten Rollen (`Admin`, `Employee`) sehen Dashboard,
 * Kunden, Mitarbeiter, Termine und den KI-Assistenten; `catalog` bleibt Admin-exklusiv
 * (unveraendertes Requirement aus `UserStories/specs/frontend-shell.openspec.md`).
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    TranslatePipe,
    AssistantChatWidgetComponent,
    NotificationBellComponent,
  ],
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  protected readonly authState = inject(AuthStateService);
  protected readonly assistantState = inject(AssistantStateService);
  protected readonly themeState = inject(ThemeState);

  readonly languages = ['de', 'en'] as const;
  readonly themes = BUILT_IN_THEMES;

  selectTheme(theme: BuiltInTheme): void {
    this.themeState.selectTheme(theme);
  }

  useLanguage(lang: string): void {
    this.translate.use(lang);
  }

  openAssistant(): void {
    this.assistantState.open();
  }

  logout(): void {
    this.authState.logout();
    void this.router.navigateByUrl('/login');
  }

  closeDropdown(event: Event): void {
    (event.currentTarget as HTMLElement | null)?.blur();
  }

  userInitial(): string {
    return this.authState.currentUser()?.displayName?.charAt(0)?.toUpperCase() ?? '';
  }

  roleTranslationKey(): string {
    const role = this.authState.currentUser()?.role;
    return role ? `crm.userCard.role.${role}` : '';
  }
}
