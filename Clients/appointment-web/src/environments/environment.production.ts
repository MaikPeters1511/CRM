import { Environment } from './environment.model';

export const environment: Environment = {
  production: true,
  // Produktion: Zugriff über API-Gateway (YARP), Pfad wird zur Deploy-Zeit konfiguriert.
  appointmentApiBaseUrl: '/api',
  customerApiBaseUrl: '/api',
  employeeApiBaseUrl: '/api',
  authApiBaseUrl: '/api',
};
