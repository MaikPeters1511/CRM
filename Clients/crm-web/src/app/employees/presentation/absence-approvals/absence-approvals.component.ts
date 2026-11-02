import { Component, OnInit, inject, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LocalDatePipe, LocalDateTimePipe, LocaleFormatService } from '../../../shared/formatting/locale-format.service';
import { AbsenceStateService } from '../../application/absence-state.service';
import { ApprovedAbsence, PendingAbsence } from '../../domain/employee.model';

type DecisionKind = 'approve' | 'reject';

interface PendingDecision {
  entry: PendingAbsence;
  kind: DecisionKind;
}

/**
 * Smart/Container-Component: Admin-Freigabe-Ansicht fuer ausstehende Abwesenheitsanfragen
 * (abwesenheit-workflow.openspec.md Abschnitt 3/4, Requirement "Abwesenheitsanfrage genehmigen
 * oder ablehnen"). Nur fuer Rolle `Admin` erreichbar (siehe `adminGuard` in `employees.routes.ts`).
 * Genehmigen/Ablehnen erfordert eine explizite Bestaetigung (inline statt `window.confirm`,
 * damit die Ansicht ohne Browser-Dialoge testbar bleibt).
 */
@Component({
  selector: 'app-absence-approvals',
  standalone: true,
  imports: [TranslatePipe, LocalDatePipe, LocalDateTimePipe],
  templateUrl: './absence-approvals.component.html',
})
export class AbsenceApprovalsComponent implements OnInit {
  protected readonly state = inject(AbsenceStateService);
  protected readonly localeFormat = inject(LocaleFormatService);

  private readonly _pendingDecision = signal<PendingDecision | null>(null);
  readonly pendingDecision = this._pendingDecision.asReadonly();

  ngOnInit(): void {
    this.state.loadPendingAbsences();
    this.state.loadApprovedAbsences?.();
  }

  approvedAbsences(): ApprovedAbsence[] {
    return this.state.approvedAbsences?.() ?? [];
  }

  deleteApprovedAbsence(entry: ApprovedAbsence): void {
    this.state.deleteAbsence?.(entry.employeeId, entry.absenceId);
  }

  isConfirming(entry: PendingAbsence): boolean {
    return this._pendingDecision()?.entry.absenceId === entry.absenceId;
  }

  confirmMessageKey(): string {
    return this._pendingDecision()?.kind === 'approve'
      ? 'employee.absenceApprovals.confirmApprove'
      : 'employee.absenceApprovals.confirmReject';
  }

  confirmMessageParams(entry: PendingAbsence): { name: string; from: string; to: string } {
    return {
      name: `${entry.employeeFirstName} ${entry.employeeLastName}`,
      from: this.localeFormat.formatDate(entry.from),
      to: this.localeFormat.formatDate(entry.to),
    };
  }

  requestApprove(entry: PendingAbsence): void {
    this._pendingDecision.set({ entry, kind: 'approve' });
  }

  requestReject(entry: PendingAbsence): void {
    this._pendingDecision.set({ entry, kind: 'reject' });
  }

  cancelDecision(): void {
    this._pendingDecision.set(null);
  }

  confirmDecision(): void {
    const decision = this._pendingDecision();
    if (!decision) {
      return;
    }

    const { entry, kind } = decision;
    const onSuccess = () => this._pendingDecision.set(null);

    if (kind === 'approve') {
      this.state.approve(entry.employeeId, entry.absenceId, onSuccess);
    } else {
      this.state.reject(entry.employeeId, entry.absenceId, onSuccess);
    }
  }
}
