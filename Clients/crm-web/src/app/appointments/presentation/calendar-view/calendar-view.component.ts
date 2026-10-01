import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthStateService } from '../../../auth/application/auth-state.service';
import { CalendarStateService } from '../../application/calendar-state.service';
import { EmployeeApiService } from '../../infrastructure/employee-api.service';
import { AppointmentListItem, AppointmentStatus, CalendarViewMode } from '../../domain/appointment.model';
import { EMPLOYEE_LOOKUP_PAGE_SIZE, EmployeeLookupItem } from '../../domain/employee.model';

const HOURS = Array.from({ length: 12 }, (_, i) => i + 8); // 08:00 - 19:00

export interface CalendarCell {
  /** Anzeigelabel der Spalte/Zeile (Uhrzeit oder Tagesnummer), fuer die Beschriftung. */
  label: string;
  dateIso: string;
  hour: number | null;
  appointments: AppointmentListItem[];
}

/**
 * Smart/Container-Component: Kalenderansicht mit Tag-/Wochen-/Monatsumschalter,
 * Mitarbeiterfilter und zeitlich positionierten Terminen (terminverwaltung.openspec.md
 * Abschnitt 4). Kalender-Grid mit `role="grid"`/`role="gridcell"` und vollstaendiger
 * Tastaturnavigation (Pfeiltasten zwischen Zeitslots, Enter zum Oeffnen).
 */
@Component({
  selector: 'app-calendar-view',
  standalone: true,
  imports: [RouterLink, TranslatePipe],
  templateUrl: './calendar-view.component.html',
})
export class CalendarViewComponent implements OnInit {
  private readonly employeeApi = inject(EmployeeApiService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthStateService);
  readonly state = inject(CalendarStateService);

  /**
   * RBAC (terminverwaltung.openspec.md, Abschnitt 3): der Server liefert einer `Employee`-Rolle
   * ausschliesslich die eigenen Termine und ignoriert jeden `employeeId`-Filter. Ein sichtbarer,
   * aber wirkungsloser Mitarbeiterfilter waere daher irreführendes UI - er wird fuer `Employee`
   * ausgeblendet. `Admin` behaelt den vollen Filter/die Gesamtuebersicht.
   */
  readonly isAdmin = this.auth.isAdmin;

  readonly viewModes: CalendarViewMode[] = ['day', 'week', 'month'];
  readonly hours = HOURS;

  private readonly _employees = signal<EmployeeLookupItem[]>([]);
  readonly employees = this._employees.asReadonly();

  private readonly _focusedRow = signal(0);
  private readonly _focusedCol = signal(0);
  readonly focusedRow = this._focusedRow.asReadonly();
  readonly focusedCol = this._focusedCol.asReadonly();

  readonly days = computed(() => {
    const { from, to } = this.state.range();
    const result: string[] = [];
    let cursor = from;
    while (cursor <= to) {
      result.push(cursor);
      cursor = addOneDay(cursor);
    }
    return result;
  });

  /** Einheitliche Grid-Struktur fuer alle drei Ansichten (Tag/Woche => Stunden x Tage, Monat => Wochen x Tage). */
  readonly rows = computed<CalendarCell[][]>(() => {
    const days = this.days();
    const appointments = this.state.appointments();

    if (this.state.viewMode() === 'month') {
      const weeks: CalendarCell[][] = [];
      for (let i = 0; i < days.length; i += 7) {
        const week = days.slice(i, i + 7).map((date) => ({
          label: date.slice(8, 10),
          dateIso: date,
          hour: null,
          appointments: appointments.filter((a) => a.date === date),
        }));
        weeks.push(week);
      }
      return weeks;
    }

    return this.hours.map((hour) =>
      days.map((date) => ({
        label: `${hour.toString().padStart(2, '0')}:00`,
        dateIso: date,
        hour,
        appointments: appointments.filter(
          (a) => a.date === date && Number(a.startTime.slice(0, 2)) === hour,
        ),
      })),
    );
  });

  ngOnInit(): void {
    this.state.loadAppointments();
    // Die Mitarbeiterliste wird nur fuer den (Admin-only) Mitarbeiterfilter benoetigt.
    if (this.isAdmin()) {
      this.employeeApi.search({ search: null, page: 1, pageSize: EMPLOYEE_LOOKUP_PAGE_SIZE }).subscribe({
        next: (result) => this._employees.set(result.items.filter((e) => e.isActive)),
        error: () => this._employees.set([]),
      });
    }
  }

  selectViewMode(mode: CalendarViewMode): void {
    this.state.setViewMode(mode);
    this._focusedRow.set(0);
    this._focusedCol.set(0);
  }

  selectEmployeeFilter(employeeId: string): void {
    this.state.setEmployeeFilter(employeeId || null);
  }

  previousPeriod(): void {
    this.state.shift(-1);
  }

  nextPeriod(): void {
    this.state.shift(1);
  }

  today(): void {
    this.state.goToToday();
  }

  statusLabelKey(status: AppointmentStatus): string {
    return `appointment.status.${status}`;
  }

  onGridKeydown(event: KeyboardEvent, rowIndex: number, colIndex: number, cell: CalendarCell): void {
    const rowsSnapshot = this.rows();
    const rowCount = rowsSnapshot.length;
    const colCount = rowsSnapshot[rowIndex]?.length ?? 0;

    switch (event.key) {
      case 'ArrowRight':
        this.moveFocus(rowIndex, Math.min(colCount - 1, colIndex + 1));
        event.preventDefault();
        break;
      case 'ArrowLeft':
        this.moveFocus(rowIndex, Math.max(0, colIndex - 1));
        event.preventDefault();
        break;
      case 'ArrowDown':
        this.moveFocus(Math.min(rowCount - 1, rowIndex + 1), colIndex);
        event.preventDefault();
        break;
      case 'ArrowUp':
        this.moveFocus(Math.max(0, rowIndex - 1), colIndex);
        event.preventDefault();
        break;
      case 'Enter':
        this.openCell(cell);
        event.preventDefault();
        break;
    }
  }

  openCell(cell: CalendarCell): void {
    const [first] = cell.appointments;
    if (first) {
      void this.router.navigate(['/appointments', first.id]);
    }
  }

  moveFocus(row: number, col: number): void {
    this._focusedRow.set(row);
    this._focusedCol.set(col);
    queueMicrotask(() => {
      document.getElementById(`calendar-cell-${row}-${col}`)?.focus();
    });
  }
}

function addOneDay(dateIso: string): string {
  const date = new Date(`${dateIso}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
}
