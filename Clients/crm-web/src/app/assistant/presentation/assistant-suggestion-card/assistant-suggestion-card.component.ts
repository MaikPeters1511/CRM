import { DatePipe } from '@angular/common';
import { Component, Input, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AppointmentSuggestion } from '../../domain/assistant.model';

/**
 * Einzelner Terminvorschlag (Kunde, Mitarbeiter, Leistung, Zeitfenster) mit Button
 * "Termin anlegen" (ki-terminassistent.openspec.md, Abschnitt 4). Deep-Link ins bestehende
 * Terminanlage-Formular mit vorausgefuellten Query-Parametern - KEINE automatische Buchung,
 * der Termin wird erst nach expliziter Bestaetigung ueber `POST /api/appointments` persistiert
 * (Requirement "Vorschlag ist unverbindlich und erfordert bestätigte Terminanlage").
 */
@Component({
  selector: 'app-assistant-suggestion-card',
  standalone: true,
  imports: [TranslatePipe, DatePipe],
  templateUrl: './assistant-suggestion-card.component.html',
})
export class AssistantSuggestionCardComponent {
  private readonly router = inject(Router);

  @Input({ required: true }) suggestion!: AppointmentSuggestion;

  applySuggestion(): void {
    const start = new Date(this.suggestion.startTime);
    const end = new Date(this.suggestion.endTime);

    void this.router.navigate(['/appointments/new'], {
      queryParams: {
        employeeId: this.suggestion.employeeId,
        serviceId: this.suggestion.serviceId,
        customerId: this.suggestion.customerId,
        date: toDateInputValue(start),
        startTime: toTimeInputValue(start),
        endTime: toTimeInputValue(end),
      },
    });
  }
}

function toDateInputValue(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function toTimeInputValue(date: Date): string {
  return date.toISOString().slice(11, 16);
}
