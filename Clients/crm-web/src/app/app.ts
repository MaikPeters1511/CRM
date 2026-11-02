import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthStateService } from './auth/application/auth-state.service';
import { AssistantChatWidgetComponent } from './assistant/presentation/assistant-chat-widget/assistant-chat-widget.component';

/**
 * Shell-Komponente: Header/Navigation/Router-Outlet fuer die konsolidierte Anwendung.
 * Zeigt die gemeinsame Navigation (Kunden/Mitarbeiter/Termine) fuer eingeloggte Nutzer
 * (Requirement "Gemeinsame Navigation", UserStories/frontend-konsolidierung.openspec.md
 * Abschnitt 3). Beide unterstuetzten Rollen (`Admin`, `Employee`) sehen alle drei Bereiche.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe, AssistantChatWidgetComponent],
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  protected readonly authState = inject(AuthStateService);

  readonly languages = ['de', 'en'] as const;

  useLanguage(lang: string): void {
    this.translate.use(lang);
  }

  logout(): void {
    this.authState.logout();
    void this.router.navigateByUrl('/login');
  }
}
