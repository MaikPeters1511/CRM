import { Environment } from './environment.model';

export const environment: Environment = {
  production: false,
  // Direkter Zugriff auf customer-service, solange kein API-Gateway existiert (siehe ADR 0001).
  customerApiBaseUrl: 'http://localhost:5149/api',
  // Direkter Zugriff auf identity-service (Login), analog zu customerApiBaseUrl.
  authApiBaseUrl: 'http://localhost:5179/api',
};
