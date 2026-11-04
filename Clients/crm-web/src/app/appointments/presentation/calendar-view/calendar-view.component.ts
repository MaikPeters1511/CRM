import { Component, DestroyRef, OnInit, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged, skip, switchMap } from 'rxjs';
import { AuthStateService } from '../../../auth/application/auth-state.service';
import { LocalTimePipe, LocaleFormatService } from '../../../shared/formatting/locale-format.service';
import { AppointmentLabelsService } from '../../application/appointment-labels.service';
import { CalendarStateService } from '../../application/calendar-state.service';
import { EmployeeApiService } from '../../infrastructure/employee-api.service';
import { AppointmentListItem, AppointmentStatus, CalendarAbsenceEntry, CalendarViewMode } from '../../domain/appointment.model';
import { EmployeeLookupItem } from '../../domain/employee.model';

/** Eine genehmigte Abwesenheit, positioniert auf einen einzelnen Kalendertag. */
export interface DayAbsenceEntry {
  entry: CalendarAbsenceEntry;
  employeeName: string;
}

const HOURS = Array.from({ length: 12 }, (_, i) => i + 8); // 08:00 - 19:00

export interface CalendarCell {
  /** Anzeigelabel der Spalte/Zeile (Uhrzeit oder Tagesnummer), fuer die Beschriftung. */
  label: string;
  dateIso: string;
  hour: number | null;
  appointments: AppointmentListItem[];
}

/**
 * Ein Termin, proportional zu seiner tatsaechlichen Dauer innerhalb eines Tages positioniert
 * (Bugfix: ein Termin wie 08:00-09:45 erschien bisher nur als kleiner Block in der 08:00-Stunde,
 * ohne dass die Dauer visuell erkennbar war). `top`/`height` sind Prozentwerte relativ zur
 * sichtbaren Tagesspanne (erste bis letzte Kalenderstunde).
 */
export interface PositionedAppointmentBlock {
  appointment: AppointmentListItem;
  top: number;
  height: number;
}

export interface DayColumn {
  dateIso: string;
  blocks: PositionedAppointmentBlock[];
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
  imports: [RouterLink, TranslatePipe, LocalTimePipe],
  templateUrl: './calendar-view.component.html',
})
export class CalendarViewComponent implements OnInit {
  private readonly employeeApi = inject(EmployeeApiService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthStateService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly translate = inject(TranslateService);
  private readonly labels = inject(AppointmentLabelsService);
  protected readonly localeFormat = inject(LocaleFormatService);
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

  private readonly _employeeFilterQuery = signal('');
  readonly employeeFilterQuery = this._employeeFilterQuery.asReadonly();
  private readonly _employees = signal<EmployeeLookupItem[]>([]);
  readonly employees = this._employees.asReadonly();

  private readonly _focusedRow = signal(0);
  private readonly _focusedCol = signal(0);
  readonly focusedRow = this._focusedRow.asReadonly();
  readonly focusedCol = this._focusedCol.asReadonly();

  /**
   * Drag-und-Drop-Verschieben (zusaetzlich zum bestehenden Verschieben-Formular in der
   * Detailansicht, das als Tastatur-/Screenreader-Alternative erhalten bleibt, da HTML5-DnD
   * selbst nicht tastaturbedienbar ist). `_draggedAppointment` haelt den Termin waehrend des
   * Ziehens; `_dropTargetKey` nur fuer die visuelle Hervorhebung der Zielzelle.
   */
  private readonly _draggedAppointment = signal<AppointmentListItem | null>(null);
  private readonly _dropTargetKey = signal<string | null>(null);

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

  readonly headerDays = computed(() => {
    const allDays = this.days();
    return this.state.viewMode() === 'month' ? allDays.slice(0, 7) : allDays;
  });

  readonly rangeLabel = computed(() => {
    this.localeFormat.lang();
    const { from, to } = this.state.range();
    if (from === to) {
      return this.localeFormat.formatDate(from, true);
    }
    return `${this.localeFormat.formatDate(from, true)} – ${this.localeFormat.formatDate(to, true)}`;
  });

  readonly stats = computed(() => {
    const list = this.state.appointments();
    return {
      total: list.length,
      confirmed: list.filter((a) => a.status === 'Confirmed').length,
      completed: list.filter((a) => a.status === 'Completed').length,
      cancelled: list.filter((a) => a.status === 'Cancelled').length,
    };
  });

  private readonly todayIso = new Date().toISOString().slice(0, 10);

  isToday(dateIso: string): boolean {
    return dateIso === this.todayIso;
  }

  dayWeekday(dateIso: string): string {
    return this.localeFormat.formatWeekdayShort(dateIso);
  }

  dayNumberAndMonth(dateIso: string): string {
    return this.localeFormat.formatDate(dateIso, false);
  }

  /**
   * Genehmigte Abwesenheiten je Tag der aktuellen Ansicht (`CalendarResponse.absenceEntries`,
   * abwesenheit-workflow.openspec.md Abschnitt 4/5). Ganztages-Eintraege, optisch getrennt
   * von den stundenbasierten Terminen gerendert (eigene Zeile oberhalb des Grids).
   */
  readonly dailyAbsences = computed<{ dateIso: string; entries: DayAbsenceEntry[] }[]>(() => {
    const absenceEntries = this.state.absenceEntries();
    return this.days().map((date) => ({
      dateIso: date,
      entries: absenceEntries
        .filter((entry) => entry.from <= date && date <= entry.to)
        .map((entry) => ({ entry, employeeName: this.employeeName(entry.employeeId) })),
    }));
  });

  /** Einheitliche Grid-Struktur fuer alle drei Ansichten (Tag/Woche => Stunden x Tage, Monat => Wochen x Tage). */
  readonly rows = computed<CalendarCell[][]>(() => {
    this.localeFormat.lang();
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

    // Ueberlappung statt reinem Start-Stunden-Vergleich (Bugfix: ein Termin wie 08:00-09:45
    // galt bisher ausschliesslich als "in der 08:00-Zelle", wurde in der 09:00-Zelle also als
    // nicht vorhanden behandelt - Klick/Enter/leer-Hinweis dort waren dadurch falsch).
    return this.hours.map((hour) =>
      days.map((date) => ({
        label: this.localeFormat.formatHour(hour),
        dateIso: date,
        hour,
        appointments: appointments.filter(
          (a) =>
            a.date === date && toMinutes(a.startTime) < (hour + 1) * 60 && toMinutes(a.endTime) > hour * 60,
        ),
      })),
    );
  });

  /**
   * Terminbloecke proportional zur Dauer positioniert (siehe `PositionedAppointmentBlock`),
   * fuer den ueberlagernden Rendering-Layer der Tag-/Wochenansicht. In der Monatsansicht leer,
   * da dort die vorhandene, stunden-unabhaengige `rows()`-Darstellung ausreicht.
   */
  readonly columns = computed<DayColumn[]>(() => {
    if (this.state.viewMode() === 'month') {
      return [];
    }
    const dayStartMinutes = this.hours[0] * 60;
    const totalMinutes = (this.hours[this.hours.length - 1] + 1) * 60 - dayStartMinutes;
    const appointments = this.state.appointments();

    return this.days().map((dateIso) => ({
      dateIso,
      blocks: appointments
        .filter((a) => a.date === dateIso)
        .map((appointment) => {
          const start = clamp(toMinutes(appointment.startTime) - dayStartMinutes, 0, totalMinutes);
          const end = clamp(toMinutes(appointment.endTime) - dayStartMinutes, 0, totalMinutes);
          return {
            appointment,
            top: (start / totalMinutes) * 100,
            // Mindesthoehe, damit auch sehr kurze Termine noch als Block sichtbar/klickbar bleiben.
            height: Math.max(((end - start) / totalMinutes) * 100, 3),
          };
        }),
    }));
  });

  constructor() {
    // Kunden-/Leistungsnamen fuer die sichtbaren Termine nachladen (Ids -> Anzeigenamen).
    effect(() => this.labels.ensureLoaded(this.state.appointments()));

    // Echte, debounced serverseitige Suche (Bug 10 Nachfolge-Fix). `skip(1)` ignoriert die
    // synchrone Erstemission von `toObservable` fuer den initialen (leeren) Suchbegriff - die
    // sinnvolle Default-Liste wird separat (ungedebounced) in `ngOnInit` geladen. Die Subscription
    // wird nur fuer Admin registriert, da das Suchfeld/der Filter fuer Employee gar nicht
    // gerendert wird (RBAC, siehe Klassendoku).
    if (this.isAdmin()) {
      toObservable(this._employeeFilterQuery)
        .pipe(
          skip(1),
          debounceTime(300),
          distinctUntilChanged(),
          switchMap((term) => this.employeeApi.search({ search: term.trim() || null, page: 1 })),
          takeUntilDestroyed(this.destroyRef),
        )
        .subscribe({
          next: (result) => this._employees.set(result.items.filter((e) => e.isActive)),
          error: () => this._employees.set([]),
        });
    }
  }

  ngOnInit(): void {
    this.state.loadAppointments();
    // Die Mitarbeiterliste wird nur fuer den (Admin-only) Mitarbeiterfilter benoetigt. Sinnvolle
    // Default-Liste beim initialen Laden (kein Suchbegriff), damit das Select nicht leer startet.
    if (this.isAdmin()) {
      this.employeeApi.search({ search: null, page: 1 }).subscribe({
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

  onEmployeeFilterQueryInput(value: string): void {
    this._employeeFilterQuery.set(value);
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

  /**
   * Vollstaendige Termindetails als Tooltip (`title`/`aria-label`) auf dem Kalender-Block, damit
   * Zeitraum/Status/Mitarbeiter auch dann vollstaendig erkennbar sind, wenn der schmale
   * Tagesspalten-Block selbst dafuer keinen Platz hat (Bugfix: Block zeigte den vollen
   * Zeitraum nicht lesbar an und hatte keinen Hover-Hinweis).
   */
  /** Kundenname des Termins, solange noch nicht aufgeloest: leerer String (kein Id-Leak im UI). */
  customerLabel(appointment: AppointmentListItem): string {
    return this.labels.customerName(appointment.customerId) ?? '';
  }

  serviceLabel(appointment: AppointmentListItem): string {
    return this.labels.serviceName(appointment.serviceId) ?? '';
  }

  appointmentTooltip(appointment: AppointmentListItem): string {
    const start = this.localeFormat.formatTime(appointment.startTime);
    const end = this.localeFormat.formatTime(appointment.endTime);
    const status = this.translate.instant(this.statusLabelKey(appointment.status));
    const parts = [`${start}–${end}`, this.serviceLabel(appointment), this.customerLabel(appointment), status].filter(
      (part) => part.length > 0,
    );
    if (this.isAdmin()) {
      parts.push(this.employeeName(appointment.employeeId));
    }
    return parts.join(' · ');
  }

  /**
   * Mitarbeitername fuer das Abwesenheits-Tooltip. Nutzt die (Admin-only) geladene
   * Mitarbeiterliste; fuer den eigenen Eintrag (auch ohne geladene Liste, z.B. Rolle
   * `Employee`) wird der eigene Anzeigename aus dem Auth-State verwendet.
   */
  employeeName(employeeId: string): string {
    const found = this._employees().find((e) => e.id === employeeId);
    if (found) {
      return `${found.firstName} ${found.lastName}`;
    }
    const currentUser = this.auth.currentUser();
    if (currentUser?.userId === employeeId) {
      return currentUser.displayName;
    }
    return employeeId;
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

  /**
   * Macht leere Zellen klick-/Enter-bar statt wirkungslos: ein bestehender Termin oeffnet die
   * Detailansicht wie bisher; eine leere Monatszelle wechselt in die Tagesansicht dieses Tages
   * (Drill-down); eine leere Stunden-Zelle fuehrt direkt zum vorausgefuellten Terminformular
   * (Datum/Uhrzeit, plus Mitarbeiter, falls ein Mitarbeiterfilter aktiv ist) - spart das erneute
   * Eintippen von Datum/Uhrzeit, die der Nutzer durch den Klick ja bereits ausgewaehlt hat.
   */
  openCell(cell: CalendarCell): void {
    const [first] = cell.appointments;
    if (first) {
      this.openAppointment(first);
      return;
    }
    if (cell.hour === null) {
      this.selectViewMode('day');
      this.state.setAnchorDate(cell.dateIso);
      return;
    }
    const queryParams: Record<string, string> = {
      date: cell.dateIso,
      startTime: `${cell.hour.toString().padStart(2, '0')}:00`,
    };
    const employeeId = this.state.employeeFilter();
    if (employeeId) {
      queryParams['employeeId'] = employeeId;
    }
    void this.router.navigate(['/appointments/new'], { queryParams });
  }

  /** Oeffnet die Detailansicht eines Termins - vom dauerproportionalen Block direkt aufgerufen. */
  openAppointment(appointment: AppointmentListItem): void {
    void this.router.navigate(['/appointments', appointment.id]);
  }

  /** Beschriftung des Interaktions-Hinweises, der beim Hover/Fokus einer leeren Zelle erscheint. */
  emptyCellHintKey(cell: CalendarCell): string {
    return cell.hour === null ? 'appointment.calendar.openDay' : 'appointment.calendar.createHere';
  }

  private cellKey(cell: CalendarCell): string {
    return `${cell.dateIso}-${cell.hour}`;
  }

  /** Visuelle Hervorhebung der aktuellen Drop-Zielzelle waehrend des Ziehens. */
  isDropTarget(cell: CalendarCell): boolean {
    return this._dropTargetKey() === this.cellKey(cell);
  }

  onAppointmentDragStart(event: DragEvent, appointment: AppointmentListItem): void {
    this._draggedAppointment.set(appointment);
    event.dataTransfer?.setData('text/plain', appointment.id);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
    }
  }

  onAppointmentDragEnd(): void {
    this._draggedAppointment.set(null);
    this._dropTargetKey.set(null);
  }

  onCellDragOver(event: DragEvent, cell: CalendarCell): void {
    if (!this._draggedAppointment()) {
      return;
    }
    event.preventDefault();
    this._dropTargetKey.set(this.cellKey(cell));
  }

  onCellDragLeave(cell: CalendarCell): void {
    if (this._dropTargetKey() === this.cellKey(cell)) {
      this._dropTargetKey.set(null);
    }
  }

  /**
   * Verschiebt den gezogenen Termin auf die Zielzelle: in der Stunden-/Wochenansicht wird auf
   * die volle Stunde der Zielzelle "eingerastet" (Datum + Stunde:00), in der Monatsansicht bleibt
   * die Uhrzeit unveraendert und nur das Datum wechselt. Die urspruengliche Dauer bleibt in
   * beiden Faellen erhalten. Kein API-Aufruf, wenn Datum/Uhrzeit dadurch unveraendert blieben.
   */
  onCellDrop(event: DragEvent, cell: CalendarCell): void {
    event.preventDefault();
    const appointment = this._draggedAppointment();
    this._draggedAppointment.set(null);
    this._dropTargetKey.set(null);
    if (!appointment) {
      return;
    }

    const target = this.computeRescheduleTarget(appointment, cell);
    if (!target) {
      return;
    }
    this.state.rescheduleAppointment(appointment.id, target, () => {});
  }

  private computeRescheduleTarget(
    appointment: AppointmentListItem,
    cell: CalendarCell,
  ): { newStart: string; newEnd: string } | null {
    const [startHour, startMinute] = appointment.startTime.split(':').map(Number);
    const [endHour, endMinute] = appointment.endTime.split(':').map(Number);
    const durationMinutes = endHour * 60 + endMinute - (startHour * 60 + startMinute);

    const newStartHour = cell.hour ?? startHour;
    const newStartMinute = cell.hour === null ? startMinute : 0;
    const newStartTotal = newStartHour * 60 + newStartMinute;
    const newEndTotal = newStartTotal + durationMinutes;

    const unchanged =
      cell.dateIso === appointment.date && newStartHour === startHour && newStartMinute === startMinute;
    if (unchanged) {
      return null;
    }

    return {
      newStart: `${cell.dateIso}T${formatHoursMinutes(newStartTotal)}:00`,
      newEnd: `${cell.dateIso}T${formatHoursMinutes(newEndTotal)}:00`,
    };
  }

  moveFocus(row: number, col: number): void {
    this._focusedRow.set(row);
    this._focusedCol.set(col);
    queueMicrotask(() => {
      document.getElementById(`calendar-cell-${row}-${col}`)?.focus();
    });
  }
}

function toMinutes(time: string): number {
  const [hours, minutes] = time.split(':').map(Number);
  return hours * 60 + minutes;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function formatHoursMinutes(totalMinutes: number): string {
  const hours = Math.floor(totalMinutes / 60) % 24;
  const minutes = totalMinutes % 60;
  return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}`;
}

function addOneDay(dateIso: string): string {
  const date = new Date(`${dateIso}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
}
