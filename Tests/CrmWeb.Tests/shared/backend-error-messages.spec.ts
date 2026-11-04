import {
  mapBackendError,
  mapBackendErrors,
} from '../../../Clients/crm-web/src/app/shared/errors/backend-error-messages';

describe('backend-error-messages', () => {
  it('maps the working-hours validation message to its i18n key', () => {
    expect(mapBackendError("The requested time window is outside the employee's available working hours.")).toBe(
      'backendErrors.outsideWorkingHours',
    );
  });

  it('maps the per-weekday StartTime/EndTime message regardless of the weekday', () => {
    expect(mapBackendError('StartTime must be before EndTime for Monday.')).toBe('backendErrors.startTimeBeforeEndTime');
  });

  it('passes unknown messages through unchanged', () => {
    expect(mapBackendErrors(['Something new happened.'])).toEqual(['Something new happened.']);
  });
});
