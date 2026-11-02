/** Granularitaet der relativen Zeitangabe einer Benachrichtigung in der Glocken-Dropdown-Liste. */
export type RelativeTimeUnit = 'justNow' | 'minutes' | 'hours' | 'days';

export interface RelativeTime {
  unit: RelativeTimeUnit;
  value: number;
}

/**
 * Reine, zustandslose Hilfsfunktion: berechnet eine grob granulare relative Zeitangabe
 * (z.B. "vor 5 Min.") aus einem ISO-Zeitstempel, ohne selbst zu uebersetzen - die
 * Praesentationsschicht uebersetzt `notification.time.<unit>` mit `{ value }` als Parameter.
 */
export function relativeTimeFrom(iso: string, now: Date = new Date()): RelativeTime {
  const diffMs = now.getTime() - new Date(iso).getTime();
  const diffMinutes = Math.floor(diffMs / 60000);

  if (diffMinutes < 1) {
    return { unit: 'justNow', value: 0 };
  }
  if (diffMinutes < 60) {
    return { unit: 'minutes', value: diffMinutes };
  }
  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) {
    return { unit: 'hours', value: diffHours };
  }
  const diffDays = Math.floor(diffHours / 24);
  return { unit: 'days', value: diffDays };
}
