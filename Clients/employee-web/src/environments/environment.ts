import { Environment } from './environment.model';

export const environment: Environment = {
  production: false,
  // Direkter Zugriff auf employee-service, solange kein API-Gateway existiert (siehe ADR 0001).
  employeeApiBaseUrl: 'http://localhost:5159/api',
};
