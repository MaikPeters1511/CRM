import { Environment } from './environment.model';

export const environment: Environment = {
  production: true,
  // Produktion: Zugriff über API-Gateway (YARP), Pfad wird zur Deploy-Zeit konfiguriert.
  customerApiBaseUrl: '/api',
  employeeApiBaseUrl: '/api',
  appointmentApiBaseUrl: '/api',
  authApiBaseUrl: '/api',
  catalogApiBaseUrl: '/api',
  assistantApiBaseUrl: '/api',
  aiServiceApiBaseUrl: '/api',
  identityApiBaseUrl: '/api',
  notificationApiBaseUrl: '/api',
};
