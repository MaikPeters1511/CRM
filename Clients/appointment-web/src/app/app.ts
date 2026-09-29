import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthStateService } from './application/auth-state.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, TranslatePipe],
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
