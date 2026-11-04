/**
 * Backend-Services liefern Validierungs-/Fachfehler als feste englische Saetze. Diese werden hier
 * auf i18n-Keys (`backendErrors.*`, DE/EN) abgebildet, damit Nutzer sie in ihrer Sprache sehen.
 * Unbekannte Meldungen werden unveraendert durchgereicht (die TranslatePipe zeigt dann den Text).
 */
const EXACT: Readonly<Record<string, string>> = {
  'The requested time window is outside the employee\'s available working hours.': 'backendErrors.outsideWorkingHours',
  'The employee already has a conflicting appointment in this time window.': 'backendErrors.employeeConflict',
  'Start must be before End.': 'backendErrors.startBeforeEnd',
  'StartTime must be before EndTime.': 'backendErrors.startTimeBeforeEndTime',
  'From must be on or before To.': 'backendErrors.fromBeforeTo',
  'Appointment not found.': 'backendErrors.appointmentNotFound',
  'Appointment is already cancelled.': 'backendErrors.alreadyCancelled',
  'Completed appointments cannot be cancelled.': 'backendErrors.completedNotCancellable',
  'Only confirmed appointments can be rescheduled.': 'backendErrors.onlyConfirmedReschedulable',
  'A cancellation reason is required.': 'backendErrors.cancellationReasonRequired',
  'You are not allowed to cancel this appointment.': 'backendErrors.notAllowedCancel',
  'You are not allowed to reschedule this appointment.': 'backendErrors.notAllowedReschedule',
  'Customer does not exist.': 'backendErrors.customerNotFound',
  'Customer is deactivated and cannot be booked.': 'backendErrors.customerDeactivated',
  'Employee does not exist.': 'backendErrors.employeeNotFound',
  'Absence request has already been decided.': 'backendErrors.absenceAlreadyDecided',
  'The absence period conflicts with existing appointments. Confirm explicitly or reschedule the appointments.':
    'backendErrors.absenceConflictsAppointments',
  'Email is already assigned to another employee.': 'backendErrors.emailTaken',
  'Email is required.': 'backendErrors.emailRequired',
  'Email must be a valid email address.': 'backendErrors.emailInvalid',
  'Either Email or Phone is required.': 'backendErrors.emailOrPhoneRequired',
  'FirstName is required.': 'backendErrors.firstNameRequired',
  'LastName is required.': 'backendErrors.lastNameRequired',
  'Name is required.': 'backendErrors.nameRequired',
  'Role is invalid.': 'backendErrors.roleInvalid',
  'ServiceId must not be empty.': 'backendErrors.serviceIdRequired',
  'ServiceName must not be empty.': 'backendErrors.serviceNameRequired',
  'Street must not be empty.': 'backendErrors.streetRequired',
  'ZipCode must not be empty.': 'backendErrors.zipCodeRequired',
  'City must not be empty.': 'backendErrors.cityRequired',
  'Country must not be empty.': 'backendErrors.countryRequired',
  'customer-service is currently unavailable. Please try again later.': 'backendErrors.serviceUnavailable',
  'employee-service is currently unavailable. Please try again later.': 'backendErrors.serviceUnavailable',
  'An unexpected error occurred.': 'backendErrors.unexpected',
  'The request could not be processed. Please try again later.': 'backendErrors.requestFailed',
};

export function mapBackendError(message: string): string {
  const exact = EXACT[message];
  if (exact) {
    return exact;
  }
  if (/^StartTime must be before EndTime for /.test(message)) {
    return 'backendErrors.startTimeBeforeEndTime';
  }
  return message;
}

export function mapBackendErrors(messages: string[]): string[] {
  return messages.map(mapBackendError);
}
