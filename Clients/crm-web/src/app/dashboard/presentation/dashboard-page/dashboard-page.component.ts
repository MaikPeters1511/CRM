import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AssistantStateService } from '../../../assistant/application/assistant-state.service';
import { LocalTimePipe } from '../../../shared/formatting/locale-format.service';
import { DashboardStateService } from '../../application/dashboard-state.service';

/**
 * Smart/Container-Component: Dashboard-Startseite (MP-CRM-Mockup-Redesign, siehe
 * UserStories/mp-crm-mockup-redesign.openspec.md). Zeigt Kennzahl-Kacheln, die Liste
 * "Anstehende Termine" und eine Vorschau-Karte des KI-Terminassistenten. Rein lesend -
 * aggregiert ausschliesslich bereits vorhandene Backend-Daten ueber `DashboardStateService`.
 */
@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [RouterLink, TranslatePipe, LocalTimePipe],
  templateUrl: './dashboard-page.component.html',
})
export class DashboardPageComponent implements OnInit {
  protected readonly state = inject(DashboardStateService);
  private readonly assistantState = inject(AssistantStateService);

  ngOnInit(): void {
    this.state.load();
  }

  openAssistant(): void {
    this.assistantState.open();
  }

  statusTranslationKey(status: string): string {
    return `appointment.status.${status}`;
  }
}
