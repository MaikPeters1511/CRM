import { Environment } from './environment.model';

export const environment: Environment = {
  production: true,
  // Produktion: Zugriff über API-Gateway (YARP), Pfad wird zur Deploy-Zeit konfiguriert.
  customerApiBaseUrl: '/api',
};
