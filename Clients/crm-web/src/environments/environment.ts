import { Environment } from './environment.model';

// Dev-Modus spricht wie der Produktions-Build ausschliesslich ueber das YARP-Gateway
// (Gateway/Crm.Gateway, fester Dev-Port aus Properties/launchSettings.json), nicht mehr
// direkt mit den einzelnen Services - konsistent mit ADR 0001 Abschnitt 4 (Gateway als
// einziger extern erreichbarer Eintrittspunkt). Erfordert, dass das Gateway laeuft
// (automatisch der Fall, wenn ueber `dotnet run --project AppHost/Crm.AppHost` gestartet).
const gatewayBaseUrl = 'https://localhost:7238/api';

export const environment: Environment = {
  production: false,
  customerApiBaseUrl: gatewayBaseUrl,
  employeeApiBaseUrl: gatewayBaseUrl,
  appointmentApiBaseUrl: gatewayBaseUrl,
  authApiBaseUrl: gatewayBaseUrl,
  catalogApiBaseUrl: gatewayBaseUrl,
  assistantApiBaseUrl: gatewayBaseUrl,
  aiServiceApiBaseUrl: gatewayBaseUrl,
  identityApiBaseUrl: gatewayBaseUrl,
  notificationApiBaseUrl: gatewayBaseUrl,
};
