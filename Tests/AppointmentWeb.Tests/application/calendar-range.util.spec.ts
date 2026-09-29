import {
  getCalendarRange,
  shiftAnchorDate,
  toListItem,
} from '../../../Clients/appointment-web/src/app/domain/calendar-range.util';

describe('calendar-range.util', () => {
  it('returns the same day for day view', () => {
    expect(getCalendarRange('day', '2024-05-15')).toEqual({ from: '2024-05-15', to: '2024-05-15' });
  });

  it('returns Monday-Sunday for week view (Wochenansicht)', () => {
    // 2024-05-15 is a Wednesday
    expect(getCalendarRange('week', '2024-05-15')).toEqual({ from: '2024-05-13', to: '2024-05-19' });
  });

  it('returns first-last day of month for month view', () => {
    expect(getCalendarRange('month', '2024-02-10')).toEqual({ from: '2024-02-01', to: '2024-02-29' });
  });

  it('shifts the anchor date by one day/week/month', () => {
    expect(shiftAnchorDate('day', '2024-05-15', 1)).toBe('2024-05-16');
    expect(shiftAnchorDate('week', '2024-05-15', 1)).toBe('2024-05-22');
    expect(shiftAnchorDate('month', '2024-05-15', 1)).toBe('2024-06-01');
    expect(shiftAnchorDate('day', '2024-05-15', -1)).toBe('2024-05-14');
  });

  it('converts a full Appointment into the flat calendar list item shape', () => {
    const item = toListItem({
      id: 'a1',
      customerId: 'c1',
      employeeId: 'e1',
      serviceId: 's1',
      start: '2024-05-15T10:00:00+00:00',
      end: '2024-05-15T11:00:00+00:00',
      status: 'Confirmed',
      cancellationReason: null,
      createdAt: '2024-05-01T00:00:00+00:00',
      createdBy: 'tester',
      modifiedAt: null,
      modifiedBy: null,
    });

    expect(item).toEqual(
      expect.objectContaining({
        id: 'a1',
        date: '2024-05-15',
        startTime: '10:00:00',
        endTime: '11:00:00',
        status: 'Confirmed',
      }),
    );
  });
});
