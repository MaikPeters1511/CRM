import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthStateService } from '../../application/auth-state.service';

/**
 * Smart/Container-Component: Login-Formular gegen identity-service (POST /api/auth/login).
 */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly state = inject(AuthStateService);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  login(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.state.login(this.form.getRawValue(), () => {
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/customers';
      void this.router.navigateByUrl(returnUrl);
    });
  }
}
