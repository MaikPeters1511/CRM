import { Environment } from './environment.model';

export const environment: Environment = {
  production: false,
  // Direkter Zugriff auf appointment-service, solange kein API-Gateway existiert (siehe ADR 0001).
  appointmentApiBaseUrl: 'http://localhost:5169/api',
  // Direkter Zugriff auf customer-service (Kunde-Autocomplete), analog zu appointmentApiBaseUrl.
  customerApiBaseUrl: 'http://localhost:5149/api',
  // Direkter Zugriff auf employee-service (Mitarbeiter-/Leistungsauswahl, Verfuegbarkeit).
  employeeApiBaseUrl: 'http://localhost:5159/api',
  // Direkter Zugriff auf identity-service (Login), analog zu appointmentApiBaseUrl.
  authApiBaseUrl: 'http://localhost:5179/api',
};
