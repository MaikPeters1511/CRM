import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';
import { LocaleFormatService } from '../../../../Clients/crm-web/src/app/shared/formatting/locale-format.service';

describe('LocaleFormatService', () => {
  function setup() {
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ lang: 'de', fallbackLang: 'de' })],
    });
    const translate = TestBed.inject(TranslateService);
    const service = TestBed.inject(LocaleFormatService);
    return { translate, service };
  }

  it('formats dates, times, hours, datetimes and weekdays in German (de)', () => {
    const { service } = setup();

    expect(service.formatDate('2026-11-02')).toBe('02.11.2026');
    expect(service.formatDate('2026-11-02', false)).toBe('02.11.');
    expect(service.formatTime('14:30:00')).toBe('14:30');
    expect(service.formatHour(8)).toBe('08:00');
    expect(service.formatDateTime('2026-11-02T14:30:00Z')).toBe('02.11.2026, 14:30');
    expect(service.formatWeekdayShort('2026-11-02')).toBe('Mo');
  });

  it('switches to English (en) format when TranslateService changes language', () => {
    const { translate, service } = setup();
    translate.use('en');

    expect(service.formatDate('2026-11-02')).toBe('11/02/2026');
    expect(service.formatDate('2026-11-02', false)).toBe('11/02');
    expect(service.formatTime('14:30:00')).toBe('2:30 PM');
    expect(service.formatTime('09:00:00')).toBe('9:00 AM');
    expect(service.formatHour(17)).toBe('5:00 PM');
    expect(service.formatDateTime('2026-11-02T14:30:00Z')).toBe('11/02/2026, 2:30 PM');
    expect(service.formatWeekdayShort('2026-11-02')).toBe('Mon');
  });
});
