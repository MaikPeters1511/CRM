import { Environment } from './environment.model';

export const environment: Environment = {
  production: false,
  // Direkter Zugriff auf customer-service, solange kein API-Gateway existiert (siehe ADR 0001).
  customerApiBaseUrl: 'http://localhost:5149/api',
};
