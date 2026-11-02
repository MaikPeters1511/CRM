import { relativeTimeFrom } from '../../../../Clients/crm-web/src/app/employees/domain/notification-relative-time.util';

describe('relativeTimeFrom', () => {
  const now = new Date('2026-10-20T12:00:00Z');

  it('returns justNow for timestamps less than a minute ago', () => {
    expect(relativeTimeFrom('2026-10-20T11:59:30Z', now)).toEqual({ unit: 'justNow', value: 0 });
  });

  it('returns minutes for timestamps less than an hour ago', () => {
    expect(relativeTimeFrom('2026-10-20T11:55:00Z', now)).toEqual({ unit: 'minutes', value: 5 });
  });

  it('returns hours for timestamps less than a day ago', () => {
    expect(relativeTimeFrom('2026-10-20T09:00:00Z', now)).toEqual({ unit: 'hours', value: 3 });
  });

  it('returns days for timestamps a day or more ago', () => {
    expect(relativeTimeFrom('2026-10-18T12:00:00Z', now)).toEqual({ unit: 'days', value: 2 });
  });
});
