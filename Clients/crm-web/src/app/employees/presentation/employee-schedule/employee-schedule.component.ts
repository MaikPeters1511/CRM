import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LocaleFormatService } from '../../../shared/formatting/locale-format.service';
import { EmployeeStateService } from '../../application/employee-state.service';
import { Weekday, WEEKDAYS, WorkingHoursSlot } from '../../domain/employee.model';

/** Zeitraster-Editor arbeitet mit vollen Stunden von 06:00 bis 20:00 Uhr. */
export const SCHEDULE_HOURS: number[] = Array.from({ length: 14 }, (_, i) => i + 6);
export const SCHEDULE_END_HOURS: number[] = Array.from({ length: 14 }, (_, i) => i + 7);
const WORKWEEK_DAYS: readonly Weekday[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'];

function slotKey(weekday: Weekday, hour: number): string {
  return `${weekday}-${hour}`;
}

function toTimeString(hour: number): string {
  return `${hour.toString().padStart(2, '0')}:00:00`;
}

/**
 * Wandelt die im Zeitraster ausgewaehlten (Wochentag, Stunde)-Zellen in
 * zusammenhaengende `WorkingHoursSlot`-Bereiche pro Wochentag um
 * (benachbarte Stunden werden zu einem Slot zusammengefasst).
 */
export function mergeSlotsToWorkingHours(selected: ReadonlySet<string>): WorkingHoursSlot[] {
  const slots: WorkingHoursSlot[] = [];
  for (const weekday of WEEKDAYS) {
    const hours = SCHEDULE_HOURS.filter((hour) => selected.has(slotKey(weekday, hour))).sort((a, b) => a - b);
    let rangeStart: number | null = null;
    let previous: number | null = null;

    const flush = (end: number) => {
      if (rangeStart !== null) {
        slots.push({ weekday, startTime: toTimeString(rangeStart), endTime: toTimeString(end + 1) });
      }
    };

    for (const hour of hours) {
      if (rangeStart === null) {
        rangeStart = hour;
      } else if (previous !== null && hour !== previous + 1) {
        flush(previous);
        rangeStart = hour;
      }
      previous = hour;
    }
    if (previous !== null) {
      flush(previous);
    }
  }
  return slots;
}

/** Kehrfunktion: initialisiert die Rasterauswahl aus bestehenden Arbeitszeit-Slots. */
export function workingHoursToSelectedSlots(slots: readonly WorkingHoursSlot[]): Set<string> {
  const selected = new Set<string>();
  for (const slot of slots) {
    const startHour = Number(slot.startTime.slice(0, 2));
    const endHour = Number(slot.endTime.slice(0, 2));
    for (let hour = startHour; hour < endHour; hour++) {
      selected.add(slotKey(slot.weekday, hour));
    }
  }
  return selected;
}

/**
 * Smart/Container-Component: Wochenplan-Editor fuer Arbeitszeiten.
 * Zeitraster vollstaendig per Tastatur bedienbar (Pfeiltasten zwischen Zeitslots).
 */
@Component({
  selector: 'app-employee-schedule',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './employee-schedule.component.html',
})
export class EmployeeScheduleComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly localeFormat = inject(LocaleFormatService);
  protected readonly state = inject(EmployeeStateService);

  protected readonly weekdays = WEEKDAYS;
  protected readonly hours = SCHEDULE_HOURS;
  protected readonly endHours = SCHEDULE_END_HOURS;

  private readonly _selected = signal<Set<string>>(new Set());
  protected readonly selected = this._selected.asReadonly();

  protected readonly totalWeeklyHours = computed(() => this._selected().size);

  private employeeId = '';

  /** Initialisiert das Zeitraster reaktiv, sobald der Mitarbeiter geladen wurde. */
  private readonly initializeGridOnLoadEffect = effect(() => {
    const employee = this.state.selectedEmployee();
    if (employee?.id === this.employeeId) {
      this._selected.set(workingHoursToSelectedSlots(employee.workingHours));
    }
  });

  ngOnInit(): void {
    this.employeeId = this.route.snapshot.paramMap.get('id') ?? '';
    this.state.loadEmployeeById(this.employeeId);
  }

  isSelected(weekday: Weekday, hour: number): boolean {
    return this._selected().has(slotKey(weekday, hour));
  }

  toggle(weekday: Weekday, hour: number): void {
    const key = slotKey(weekday, hour);
    this._selected.update((set) => {
      const next = new Set(set);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  }

  isDayActive(weekday: Weekday): boolean {
    return this.hours.some((h) => this._selected().has(slotKey(weekday, h)));
  }

  toggleDay(weekday: Weekday): void {
    if (this.isDayActive(weekday)) {
      this.clearDay(weekday);
    } else {
      this.setDayRange(weekday, 8, 17);
    }
  }

  clearDay(weekday: Weekday): void {
    this._selected.update((set) => {
      const next = new Set(set);
      for (const h of this.hours) {
        next.delete(slotKey(weekday, h));
      }
      return next;
    });
  }

  dayStartHour(weekday: Weekday): number {
    const active = this.hours.filter((h) => this._selected().has(slotKey(weekday, h)));
    return active.length > 0 ? Math.min(...active) : 8;
  }

  dayEndHour(weekday: Weekday): number {
    const active = this.hours.filter((h) => this._selected().has(slotKey(weekday, h)));
    return active.length > 0 ? Math.max(...active) + 1 : 17;
  }

  setDayRange(weekday: Weekday, startVal: number | string, endVal: number | string): void {
    const start = Number(startVal);
    const end = Number(endVal);
    if (Number.isNaN(start) || Number.isNaN(end) || start >= end) {
      return;
    }
    this._selected.update((set) => {
      const next = new Set(set);
      for (const h of this.hours) {
        next.delete(slotKey(weekday, h));
      }
      for (let h = start; h < end; h++) {
        if (this.hours.includes(h)) {
          next.add(slotKey(weekday, h));
        }
      }
      return next;
    });
  }

  daySummary(weekday: Weekday): string {
    const slots = mergeSlotsToWorkingHours(this._selected()).filter((s) => s.weekday === weekday);
    if (slots.length === 0) {
      return '';
    }
    return slots
      .map((s) => `${this.localeFormat.formatTime(s.startTime)} – ${this.localeFormat.formatTime(s.endTime)}`)
      .join(', ');
  }

  applyWorkweekPreset(startHour: number, endHour: number): void {
    const next = new Set<string>();
    for (const day of WORKWEEK_DAYS) {
      for (let h = startHour; h < endHour; h++) {
        next.add(slotKey(day, h));
      }
    }
    this._selected.set(next);
  }

  clearAll(): void {
    this._selected.set(new Set());
  }

  formatHour(hour: number): string {
    return this.localeFormat.formatHour(hour);
  }

  /** Pfeiltasten-Navigation zwischen den Zeitslot-Zellen (roving tabindex per Fokus). */
  onKeydown(event: KeyboardEvent, rowIndex: number, colIndex: number): void {
    const arrowKeys = ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'];
    if (!arrowKeys.includes(event.key)) {
      return;
    }
    event.preventDefault();

    let nextRow = rowIndex;
    let nextCol = colIndex;
    if (event.key === 'ArrowUp') nextRow = Math.max(0, rowIndex - 1);
    if (event.key === 'ArrowDown') nextRow = Math.min(this.weekdays.length - 1, rowIndex + 1);
    if (event.key === 'ArrowLeft') nextCol = Math.max(0, colIndex - 1);
    if (event.key === 'ArrowRight') nextCol = Math.min(this.hours.length - 1, colIndex + 1);

    const target = (event.currentTarget as HTMLElement)
      ?.closest('[role="grid"]')
      ?.querySelector<HTMLElement>(`[data-row="${nextRow}"][data-col="${nextCol}"]`);
    target?.focus();
  }

  save(): void {
    const slots = mergeSlotsToWorkingHours(this._selected());
    this.state.setWorkingHours(this.employeeId, slots, () => this.router.navigate(['/employees']));
  }

  cancel(): void {
    this.router.navigate(['/employees']);
  }
}
