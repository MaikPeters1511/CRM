import { Injectable, Pipe, PipeTransform, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export type SupportedLocale = 'de' | 'en';

const DE_WEEKDAYS_SHORT = ['So', 'Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa'] as const;
const EN_WEEKDAYS_SHORT = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'] as const;

/**
 * Zentraler Formatierungs-Service fuer sprachabhaengige Datums- und Uhrzeitdarstellung.
 * Reagiert automatisch auf Sprachwechsel in `TranslateService`:
 * - Deutsch (`de`): Datum `TT.MM.JJJJ` (bzw. `TT.MM.`), Uhrzeit 24-Stunden `HH:mm`
 * - Englisch (`en`): Datum `MM/DD/YYYY` (bzw. `MM/DD`), Uhrzeit 12-Stunden `h:mm AM/PM`
 */
@Injectable({
  providedIn: 'root',
})
export class LocaleFormatService {
  private readonly translate = inject(TranslateService, { optional: true });
  private readonly _lang = signal<SupportedLocale>(this.resolveInitialLang());

  readonly lang = this._lang.asReadonly();

  constructor() {
    this.syncDocumentLang(this._lang());
    this.translate?.onLangChange?.subscribe((event) => {
      const next: SupportedLocale = event.lang === 'en' ? 'en' : 'de';
      this._lang.set(next);
      this.syncDocumentLang(next);
    });
  }

  private resolveInitialLang(): SupportedLocale {
    const current = this.translate?.getCurrentLang() || this.translate?.getFallbackLang() || 'de';
    return current === 'en' ? 'en' : 'de';
  }

  private syncDocumentLang(lang: SupportedLocale): void {
    if (typeof document !== 'undefined' && document.documentElement) {
      document.documentElement.lang = lang;
    }
  }

  /**
   * Formatiert ein Datum (`YYYY-MM-DD`, ISO-Zeitstempel oder `Date`) sprachabhaengig:
   * - `de`: `02.11.2026` (bzw. `02.11.` bei `withYear = false`)
   * - `en`: `11/02/2026` (bzw. `11/02` bei `withYear = false`)
   */
  formatDate(value: string | Date | null | undefined, withYear = true): string {
    if (!value) {
      return '';
    }

    let year = '';
    let month = '';
    let day = '';

    if (typeof value === 'string') {
      const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value.trim());
      if (match) {
        year = match[1];
        month = match[2];
        day = match[3];
      } else {
        const parsed = new Date(value);
        if (Number.isNaN(parsed.getTime())) {
          return value;
        }
        year = parsed.getFullYear().toString();
        month = (parsed.getMonth() + 1).toString().padStart(2, '0');
        day = parsed.getDate().toString().padStart(2, '0');
      }
    } else {
      if (Number.isNaN(value.getTime())) {
        return '';
      }
      year = value.getFullYear().toString();
      month = (value.getMonth() + 1).toString().padStart(2, '0');
      day = value.getDate().toString().padStart(2, '0');
    }

    if (this.currentLocale() === 'en') {
      return withYear ? `${month}/${day}/${year}` : `${month}/${day}`;
    }
    return withYear ? `${day}.${month}.${year}` : `${day}.${month}.`;
  }

  /**
   * Formatiert eine Uhrzeit (`HH:mm`, `HH:mm:ss`, ISO-String oder `Date`) sprachabhaengig:
   * - `de`: 24-Stunden-Format `14:30`
   * - `en`: 12-Stunden-Format `2:30 PM`
   */
  formatTime(value: string | Date | null | undefined): string {
    if (!value) {
      return '';
    }

    let hours: number;
    let minutes: number;

    if (typeof value === 'string') {
      const trimmed = value.trim();
      const timeMatch = /^(\d{1,2}):(\d{2})(?::\d{2})?/.exec(trimmed);
      const isoMatch = /^\d{4}-\d{2}-\d{2}T(\d{2}):(\d{2})/.exec(trimmed);

      if (timeMatch) {
        hours = Number(timeMatch[1]);
        minutes = Number(timeMatch[2]);
      } else if (isoMatch) {
        hours = Number(isoMatch[1]);
        minutes = Number(isoMatch[2]);
      } else {
        const parsed = new Date(trimmed);
        if (Number.isNaN(parsed.getTime())) {
          return value;
        }
        hours = parsed.getHours();
        minutes = parsed.getMinutes();
      }
    } else {
      if (Number.isNaN(value.getTime())) {
        return '';
      }
      hours = value.getHours();
      minutes = value.getMinutes();
    }

    const mm = minutes.toString().padStart(2, '0');
    if (this.currentLocale() === 'en') {
      const period = hours >= 12 ? 'PM' : 'AM';
      const h12 = hours % 12 === 0 ? 12 : hours % 12;
      return `${h12}:${mm} ${period}`;
    }
    return `${hours.toString().padStart(2, '0')}:${mm}`;
  }

  /**
   * Formatiert eine volle Stunde (`0`..`24`) sprachabhaengig:
   * - `de`: `08:00`, `17:00`
   * - `en`: `8:00 AM`, `5:00 PM`
   */
  formatHour(hour: number): string {
    return this.formatTime(`${hour.toString().padStart(2, '0')}:00`);
  }

  /**
   * Formatiert einen kombinierten Zeitstempel (`YYYY-MM-DDTHH:mm:ss...` oder `YYYY-MM-DD`) sprachabhaengig:
   * - `de`: `02.11.2026, 14:30`
   * - `en`: `11/02/2026, 2:30 PM`
   */
  formatDateTime(value: string | Date | null | undefined): string {
    if (!value) {
      return '';
    }
    if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value.trim())) {
      return this.formatDate(value, true);
    }
    const datePart = this.formatDate(value, true);
    const timePart = this.formatTime(value);
    return datePart && timePart ? `${datePart}, ${timePart}` : datePart || timePart;
  }

  /**
   * Liefert das kurze Wochentagskuerzel fuer ein ISO-Datum (`YYYY-MM-DD`):
   * - `de`: `Mo`..`So`
   * - `en`: `Mon`..`Sun`
   */
  formatWeekdayShort(dateIso: string): string {
    const date = new Date(`${dateIso}T00:00:00Z`);
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    const dayIndex = date.getUTCDay();
    return this.currentLocale() === 'en'
      ? (EN_WEEKDAYS_SHORT[dayIndex] ?? '')
      : (DE_WEEKDAYS_SHORT[dayIndex] ?? '');
  }

  private currentLocale(): SupportedLocale {
    const active = this.translate?.getCurrentLang() || this._lang();
    return active === 'en' ? 'en' : 'de';
  }
}

@Pipe({
  name: 'localDate',
  standalone: true,
  pure: false,
})
export class LocalDatePipe implements PipeTransform {
  private readonly localeFormat = inject(LocaleFormatService);

  transform(value: string | Date | null | undefined, withYear = true): string {
    return this.localeFormat.formatDate(value, withYear);
  }
}

@Pipe({
  name: 'localTime',
  standalone: true,
  pure: false,
})
export class LocalTimePipe implements PipeTransform {
  private readonly localeFormat = inject(LocaleFormatService);

  transform(value: string | Date | null | undefined): string {
    return this.localeFormat.formatTime(value);
  }
}

@Pipe({
  name: 'localDateTime',
  standalone: true,
  pure: false,
})
export class LocalDateTimePipe implements PipeTransform {
  private readonly localeFormat = inject(LocaleFormatService);

  transform(value: string | Date | null | undefined): string {
    return this.localeFormat.formatDateTime(value);
  }
}
