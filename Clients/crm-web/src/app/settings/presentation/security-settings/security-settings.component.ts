import { Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { SecuritySettingsState } from '../../application/security-settings.state';

/**
 * Smart/Container-Component: Sicherheits-Einstellungen (Passwort-Richtlinie, MFA-Pflicht,
 * vertrauenswuerdige Geraete, Audit-Log mit CSV-Export) gegen die Identity-Security-API, die
 * parallel im Branch `identity-mfa-passkeys-audit` entsteht. Zeigt einen Error-State, solange
 * der Endpunkt dort noch nicht existiert.
 */
@Component({
  selector: 'app-security-settings',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './security-settings.component.html',
})
export class SecuritySettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  protected readonly state = inject(SecuritySettingsState);

  protected readonly form = this.fb.nonNullable.group({
    mfaRequired: [false],
    minPasswordLength: [8],
    passwordRequiresSpecialChar: [false],
    sessionTimeoutMinutes: [30],
  });

  constructor() {
    effect(() => {
      const policy = this.state.policy();
      if (policy) {
        this.form.setValue(policy);
      }
    });
  }

  ngOnInit(): void {
    this.state.load();
  }

  savePolicy(): void {
    this.state.savePolicy(this.form.getRawValue(), () => {});
  }

  revokeDevice(deviceId: string): void {
    this.state.revokeDevice(deviceId);
  }

  exportAuditLog(): void {
    const csv = this.state.exportAuditLogAsCsv();
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'audit-log.csv';
    link.click();
    URL.revokeObjectURL(url);
  }
}
